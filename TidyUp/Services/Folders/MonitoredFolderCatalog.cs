using System.Collections.ObjectModel;
using TidyUp.Data.Repositories;
using TidyUp.Models.Folders;
using TidyUp.Services.Monitoring;

namespace TidyUp.Services.Folders;

/// <summary>
/// Single source of truth for monitored folders across the application.
/// Aggregates folder configurations across all rules, canonicalizes paths,
/// performs physical existence/permission validation, and coordinates with watcher health.
/// </summary>
public class MonitoredFolderCatalog : IMonitoredFolderCatalog
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IFolderPathNormalizer _pathNormalizer;
    private readonly IFolderValidator _folderValidator;
    private readonly IWatcherHealthMonitor _watcherHealthMonitor;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private readonly ObservableCollection<MonitoredFolderState> _foldersCollection = new();
    private bool _disposed;

    public ReadOnlyObservableCollection<MonitoredFolderState> Folders { get; }
    public MonitoredFolderSummary Summary { get; private set; } = new();

    public event EventHandler<FolderCatalogChangedEventArgs>? CatalogChanged;

    public MonitoredFolderCatalog(
        IRuleRepository ruleRepository,
        IFolderPathNormalizer pathNormalizer,
        IFolderValidator folderValidator,
        IWatcherHealthMonitor watcherHealthMonitor)
    {
        _ruleRepository = ruleRepository;
        _pathNormalizer = pathNormalizer;
        _folderValidator = folderValidator;
        _watcherHealthMonitor = watcherHealthMonitor;

        Folders = new ReadOnlyObservableCollection<MonitoredFolderState>(_foldersCollection);
        _watcherHealthMonitor.FolderHealthChanged += OnWatcherHealthChanged;
        _ruleRepository.RulesChanged += OnRulesChanged;
        if (_ruleRepository is RuleRepository)
        {
            RuleRepository.AnyRulesChanged += OnAnyRulesChanged;
        }
    }

    private async void OnRulesChanged(object? sender, RulesChangedEventArgs e)
    {
        if (_disposed) return;
        try
        {
            await RefreshAsync();
        }
        catch
        {
            // Ignore background refresh failures
        }
    }

    private void OnAnyRulesChanged(object? sender, RulesChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _ruleRepository)) return;
        OnRulesChanged(sender, e);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var rules = await _ruleRepository.GetAllAsync();

            var folderDict = new Dictionary<string, (
                string DisplayPath,
                List<Guid> RuleIds,
                List<string> RuleNames,
                int EnabledCount,
                bool IncludeSubfolders,
                HashSet<string> Exclusions)>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in rules)
            {
                foreach (var folder in rule.MonitoredFolders)
                {
                    if (string.IsNullOrWhiteSpace(folder.Path))
                        continue;

                    string canonicalPath;
                    if (!_pathNormalizer.TryNormalize(folder.Path, out canonicalPath, out _))
                    {
                        canonicalPath = folder.Path.Trim();
                    }

                    if (!folderDict.TryGetValue(canonicalPath, out var entry))
                    {
                        entry = (
                            DisplayPath: canonicalPath,
                            RuleIds: new List<Guid>(),
                            RuleNames: new List<string>(),
                            EnabledCount: 0,
                            IncludeSubfolders: false,
                            Exclusions: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        );
                        folderDict[canonicalPath] = entry;
                    }

                    if (!entry.RuleIds.Contains(rule.Id))
                    {
                        entry.RuleIds.Add(rule.Id);
                        entry.RuleNames.Add(rule.Name);
                    }

                    if (rule.IsEnabled)
                    {
                        entry.EnabledCount++;
                    }

                    if (folder.IncludeSubfolders)
                    {
                        entry.IncludeSubfolders = true;
                    }

                    foreach (var pattern in folder.ExclusionPatterns)
                    {
                        if (!string.IsNullOrWhiteSpace(pattern))
                        {
                            entry.Exclusions.Add(pattern.Trim());
                        }
                    }

                    folderDict[canonicalPath] = entry;
                }
            }

            var states = new List<MonitoredFolderState>();

            foreach (var kvp in folderDict)
            {
                var path = kvp.Key;
                var entry = kvp.Value;

                var validation = _folderValidator.Validate(path);
                WatcherHealthStatus watcherStatus;
                string? detail;

                if (validation.Status == FolderValidationStatus.Accessible)
                {
                    var isPaused = entry.EnabledCount == 0;
                    _watcherHealthMonitor.RegisterFolder(path, isPaused);

                    var report = _watcherHealthMonitor.GetFolderHealth(path);
                    watcherStatus = report?.Status ?? (isPaused ? WatcherHealthStatus.Paused : WatcherHealthStatus.Healthy);
                    detail = report?.StatusDetail;
                }
                else
                {
                    _watcherHealthMonitor.UnregisterFolder(path);
                    watcherStatus = validation.Status == FolderValidationStatus.Missing
                        ? WatcherHealthStatus.Disconnected
                        : WatcherHealthStatus.Error;
                    detail = validation.Message;
                }

                var state = new MonitoredFolderState
                {
                    NormalizedPath = path,
                    DisplayPath = entry.DisplayPath,
                    Exists = validation.Exists,
                    HasReadPermission = validation.HasReadPermission,
                    HasWritePermission = validation.HasWritePermission,
                    IsSystemProtected = validation.IsSystemProtected,
                    ValidationStatus = validation.Status,
                    WatcherHealth = watcherStatus,
                    StatusDetail = detail,
                    LinkedRuleIds = entry.RuleIds.ToArray(),
                    LinkedRuleNames = entry.RuleNames.ToArray(),
                    EnabledRuleCount = entry.EnabledCount,
                    IncludeSubfolders = entry.IncludeSubfolders,
                    ExclusionPatterns = entry.Exclusions.OrderBy(x => x).ToArray(),
                    LastEvaluatedUtc = DateTime.UtcNow
                };

                states.Add(state);
            }

            // Clean up watchers for folders no longer referenced by any rule
            var allWatchedPaths = _watcherHealthMonitor.GetAllFolderHealth().Keys.ToList();
            foreach (var watched in allWatchedPaths)
            {
                if (!folderDict.ContainsKey(watched))
                {
                    _watcherHealthMonitor.UnregisterFolder(watched);
                }
            }

            var summary = new MonitoredFolderSummary
            {
                TotalFolders = states.Count,
                HealthyFolders = states.Count(s => s.ValidationStatus == FolderValidationStatus.Accessible && s.WatcherHealth == WatcherHealthStatus.Healthy),
                MissingFolders = states.Count(s => s.ValidationStatus == FolderValidationStatus.Missing),
                AccessDeniedFolders = states.Count(s => s.ValidationStatus == FolderValidationStatus.AccessDenied),
                DegradedFolders = states.Count(s => s.WatcherHealth == WatcherHealthStatus.Degraded),
                PausedFolders = states.Count(s => s.WatcherHealth == WatcherHealthStatus.Paused),
                DistinctAlertCount = states.Count(s => s.HasAlert)
            };

            RunOnUi(() =>
            {
                _foldersCollection.Clear();
                foreach (var state in states.OrderBy(s => s.NormalizedPath))
                {
                    _foldersCollection.Add(state);
                }
            });

            Summary = summary;
            CatalogChanged?.Invoke(this, new FolderCatalogChangedEventArgs
            {
                Folders = states,
                Summary = summary
            });
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public MonitoredFolderState? GetFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var normalized = _pathNormalizer.TryNormalize(path, out var norm, out _) ? norm : path.Trim();
        return _foldersCollection.FirstOrDefault(f => string.Equals(f.NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private void OnWatcherHealthChanged(object? sender, FolderHealthReport report)
    {
        if (report == null || string.IsNullOrWhiteSpace(report.Path)) return;

        var normalized = _pathNormalizer.TryNormalize(report.Path, out var norm, out _) ? norm : report.Path.Trim();

        RunOnUi(() =>
        {
            var existing = _foldersCollection.FirstOrDefault(f =>
                string.Equals(f.NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                var index = _foldersCollection.IndexOf(existing);
                var updated = new MonitoredFolderState
                {
                    NormalizedPath = existing.NormalizedPath,
                    DisplayPath = existing.DisplayPath,
                    Exists = existing.Exists,
                    HasReadPermission = existing.HasReadPermission,
                    HasWritePermission = existing.HasWritePermission,
                    IsSystemProtected = existing.IsSystemProtected,
                    ValidationStatus = existing.ValidationStatus,
                    WatcherHealth = report.Status,
                    StatusDetail = report.StatusDetail ?? existing.StatusDetail,
                    LinkedRuleIds = existing.LinkedRuleIds,
                    LinkedRuleNames = existing.LinkedRuleNames,
                    EnabledRuleCount = existing.EnabledRuleCount,
                    IncludeSubfolders = existing.IncludeSubfolders,
                    ExclusionPatterns = existing.ExclusionPatterns,
                    LastEvaluatedUtc = DateTime.UtcNow
                };

                _foldersCollection[index] = updated;

                Summary = new MonitoredFolderSummary
                {
                    TotalFolders = _foldersCollection.Count,
                    HealthyFolders = _foldersCollection.Count(s => s.ValidationStatus == FolderValidationStatus.Accessible && s.WatcherHealth == WatcherHealthStatus.Healthy),
                    MissingFolders = _foldersCollection.Count(s => s.ValidationStatus == FolderValidationStatus.Missing),
                    AccessDeniedFolders = _foldersCollection.Count(s => s.ValidationStatus == FolderValidationStatus.AccessDenied),
                    DegradedFolders = _foldersCollection.Count(s => s.WatcherHealth == WatcherHealthStatus.Degraded),
                    PausedFolders = _foldersCollection.Count(s => s.WatcherHealth == WatcherHealthStatus.Paused),
                    DistinctAlertCount = _foldersCollection.Count(s => s.HasAlert)
                };

                CatalogChanged?.Invoke(this, new FolderCatalogChangedEventArgs
                {
                    Folders = _foldersCollection.ToList(),
                    Summary = Summary
                });
            }
        });
    }

    private static void RunOnUi(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcherHealthMonitor.FolderHealthChanged -= OnWatcherHealthChanged;
        _ruleRepository.RulesChanged -= OnRulesChanged;
        RuleRepository.AnyRulesChanged -= OnAnyRulesChanged;
        _syncLock.Dispose();
    }
}

