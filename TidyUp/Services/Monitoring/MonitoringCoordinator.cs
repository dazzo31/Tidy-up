using System.Collections.Concurrent;
using System.IO;
using TidyUp.Data.Repositories;
using TidyUp.Models;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Processing;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Services.State;

namespace TidyUp.Services.Monitoring;

/// <summary>
/// Application-owned monitoring coordinator that loads enabled rules, activates
/// FileMonitorService, routes detected files through safe simulation and batch execution,
/// and responds to rule changes (TASK-REM-24 / TU-01).
/// </summary>
public class MonitoringCoordinator : IMonitoringCoordinator
{
    private readonly IFileMonitorService _fileMonitorService;
    private readonly IRuleRepository _ruleRepository;
    private readonly IRuleEngine _ruleEngine;
    private readonly IExecutionPlanGenerator _planGenerator;
    private readonly IBatchProcessingCoordinator _batchCoordinator;
    private readonly IApplicationStateManager _applicationStateManager;
    private readonly AppSettings _appSettings;
    private readonly IToastNotificationService? _toastService;

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTime> _inFlightFiles = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _coordinatorCts;
    private bool _isRunning;
    private bool _disposed;

    public bool IsRunning => _isRunning;

    public MonitoringCoordinator(
        IFileMonitorService fileMonitorService,
        IRuleRepository ruleRepository,
        IRuleEngine ruleEngine,
        IExecutionPlanGenerator planGenerator,
        IBatchProcessingCoordinator batchCoordinator,
        IApplicationStateManager applicationStateManager,
        AppSettings appSettings,
        IToastNotificationService? toastService = null)
    {
        _fileMonitorService = fileMonitorService ?? throw new ArgumentNullException(nameof(fileMonitorService));
        _ruleRepository = ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _planGenerator = planGenerator ?? throw new ArgumentNullException(nameof(planGenerator));
        _batchCoordinator = batchCoordinator ?? throw new ArgumentNullException(nameof(batchCoordinator));
        _applicationStateManager = applicationStateManager ?? throw new ArgumentNullException(nameof(applicationStateManager));
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _toastService = toastService;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_isRunning)
                return;

            _coordinatorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _fileMonitorService.FileDetected += OnFileDetected;

            var rules = (await _ruleRepository.GetAllAsync())
                .Where(r => r.IsEnabled && r.TriggerType != RuleTriggerType.ManualOnly)
                .OrderBy(r => r.ExecutionOrder)
                .ToList();

            await _fileMonitorService.StartAsync(rules, _coordinatorCts.Token);
            _isRunning = true;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _fileMonitorService.FileDetected -= OnFileDetected;
            _coordinatorCts?.Cancel();

            await _fileMonitorService.StopAsync();

            _coordinatorCts?.Dispose();
            _coordinatorCts = null;
            _inFlightFiles.Clear();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task RefreshRulesAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (!_isRunning)
                return;

            var rules = (await _ruleRepository.GetAllAsync())
                .Where(r => r.IsEnabled && r.TriggerType != RuleTriggerType.ManualOnly)
                .OrderBy(r => r.ExecutionOrder)
                .ToList();

            await _fileMonitorService.UpdateRulesAsync(rules);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async void OnFileDetected(object? sender, FileDetectedEventArgs e)
    {
        try
        {
            if (_coordinatorCts?.IsCancellationRequested == true || !_isRunning)
                return;

            await ProcessFileDetectedAsync(e.FileInfo.FullName, e.SourceFolder);
        }
        catch
        {
            // Fail-safe error isolation so watcher event pipeline never unhandled faults
        }
    }

    public async Task ProcessFileDetectedAsync(string filePath, string sourceFolder)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        if (_fileMonitorService.IsPaused)
            return;

        // Deduplicate rapid in-flight notifications for the same file path
        var canonicalPath = FileMonitorService.CanonicalizePath(filePath);
        if (!_inFlightFiles.TryAdd(canonicalPath, DateTime.UtcNow))
        {
            if (_inFlightFiles.TryGetValue(canonicalPath, out var timestamp) && DateTime.UtcNow - timestamp < TimeSpan.FromSeconds(2))
            {
                return; // Already being processed
            }
            _inFlightFiles[canonicalPath] = DateTime.UtcNow;
        }

        try
        {
            var rules = (await _ruleRepository.GetAllAsync())
                .Where(r => r.IsEnabled && r.TriggerType != RuleTriggerType.ManualOnly)
                .OrderBy(r => r.ExecutionOrder)
                .ToList();

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return;

            foreach (var rule in rules)
            {
                if (!fileInfo.Exists)
                    break;

                // Check folder eligibility (path and exclusions)
                var matchingFolder = rule.MonitoredFolders.FirstOrDefault(f => _fileMonitorService.IsFileEligibleForFolder(filePath, f));
                if (matchingFolder == null)
                    continue;

                // Evaluate rule conditions
                if (!_ruleEngine.MatchesConditions(rule, fileInfo))
                    continue;

                // Generate simulated execution plan for this file
                var plan = await _planGenerator.GeneratePlanForFileAsync(rule, filePath, matchingFolder.Path, _coordinatorCts?.Token ?? default);
                if (plan.PlannedActions.Count == 0)
                    continue;

                // Issue scoped authorization context (disallowing unattended permanent deletion fallback)
                var batchId = Guid.NewGuid();
                var authContext = new ExecutionAuthorizationContext
                {
                    PlanId = plan.PlanId,
                    AuthorizePermanentDeletion = false,
                    AuthorizedActionIds = plan.PlannedActions.Select(a => a.Id).ToHashSet(),
                    AuthorizedPermanentDeletionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    AuthorizedOverwritePaths = plan.PlannedActions.Where(a => !string.IsNullOrEmpty(a.TargetPath)).Select(a => a.TargetPath!).ToHashSet(StringComparer.OrdinalIgnoreCase)
                };

                foreach (var action in plan.PlannedActions)
                {
                    action.AuthorizationContext = authContext;
                }

                // Execute through batch coordinator enforcing admission lease, journaling, and displaced backups
                var batchResult = await _batchCoordinator.ExecuteBatchAsync(plan.PlannedActions, batchId, null, _coordinatorCts?.Token ?? default);

                if (batchResult.SucceededCount > 0 && _appSettings.ShowSuccessNotifications && _toastService != null)
                {
                    _toastService.ShowNotification("TidyUp", $"Processed {Path.GetFileName(filePath)} using rule '{rule.Name}'", ToastNotificationType.Success);
                }

                if (batchResult.FailedCount > 0 && _appSettings.ShowErrorNotifications && _toastService != null)
                {
                    _toastService.ShowNotification("TidyUp Error", $"Failed processing {Path.GetFileName(filePath)}: {batchResult.FailedCount} error(s)", ToastNotificationType.Error);
                }

                // Stop evaluating subsequent rules if rule specifies StopProcessingRules
                if (rule.StopProcessingAfterMatch)
                    break;
            }
        }
        finally
        {
            _inFlightFiles.TryRemove(canonicalPath, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _isRunning = false;
        _fileMonitorService.FileDetected -= OnFileDetected;
        _coordinatorCts?.Cancel();
        _coordinatorCts?.Dispose();
        _coordinatorCts = null;
        _lifecycleLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
