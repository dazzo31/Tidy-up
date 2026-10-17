using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using TidyUp.Data.Entities;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Models.Folders;
using TidyUp.Services;
using TidyUp.Services.Folders;
using TidyUp.Services.Monitoring;
using TidyUp.Services.Watcher;

namespace TidyUp.ViewModels;

public class MonitoredFolderItem : ObservableObject
{
    public string Path { get; set; } = string.Empty;
    public string DisplayName => System.IO.Path.GetFileName(Path) is { Length: > 0 } name ? name : Path;
    public bool Exists => Directory.Exists(Path);
    public bool IncludeSubfolders { get; set; }
    public int RuleCount { get; set; }
    public string AssociatedRules { get; set; } = string.Empty;
    public List<string> ExclusionPatterns { get; set; } = new();

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanScan));
                OnPropertyChanged(nameof(ScanTooltip));
            }
        }
    }

    /// <summary>False while a scan runs and for any folder that cannot be read (missing, denied, protected, errored).</summary>
    public bool CanScan => !IsScanning && FolderStatusPresentation.CanScan(Status);

    public string ScanTooltip => IsScanning ? "Scan in progress" : FolderStatusPresentation.ScanTooltip(Status);

    private WatcherHealthStatus _healthStatus = WatcherHealthStatus.Healthy;
    public WatcherHealthStatus HealthStatus
    {
        get => _healthStatus;
        set
        {
            if (SetProperty(ref _healthStatus, value))
            {
                NotifyStatusChanged();
            }
        }
    }

    private FolderValidationStatus _validationStatus = FolderValidationStatus.Accessible;
    public FolderValidationStatus ValidationStatus
    {
        get => _validationStatus;
        set
        {
            if (SetProperty(ref _validationStatus, value))
            {
                NotifyStatusChanged();
            }
        }
    }

    private bool _isSystemProtected;
    public bool IsSystemProtected
    {
        get => _isSystemProtected;
        set
        {
            if (SetProperty(ref _isSystemProtected, value))
            {
                NotifyStatusChanged();
            }
        }
    }

    /// <summary>The same status the Folders hub shows for this folder.</summary>
    public FolderStatus Status => FolderStatusResolver.Resolve(ValidationStatus, HealthStatus, IsSystemProtected);

    private void NotifyStatusChanged()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(ScanTooltip));
        OnPropertyChanged(nameof(HealthStatusText));
        OnPropertyChanged(nameof(HealthTooltip));
    }

    private string? _healthStatusDetail;
    public string? HealthStatusDetail
    {
        get => _healthStatusDetail;
        set
        {
            if (SetProperty(ref _healthStatusDetail, value))
            {
                OnPropertyChanged(nameof(HealthTooltip));
            }
        }
    }

    public string HealthStatusText => FolderStatusPresentation.Text(Status);

    public string HealthTooltip => !string.IsNullOrWhiteSpace(HealthStatusDetail)
        ? $"{HealthStatusText}: {HealthStatusDetail}"
        : FolderStatusPresentation.Description(Status);

    public void UpdateHealth(FolderHealthReport report)
    {
        HealthStatus = report.Status;
        HealthStatusDetail = report.StatusDetail;
    }

    public void UpdateHealth(WatcherHealthStatus status, string? detail = null)
    {
        HealthStatus = status;
        HealthStatusDetail = detail;
    }
}

public class RecentActivityItem
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("g");
    public string RelativeTime => FormatRelativeTime(Timestamp);
    public string RuleName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public string ActionPerformed { get; set; } = string.Empty;
    public string? ResultPath { get; set; }
    public string Status { get; set; } = "Success";
    public string? ErrorMessage { get; set; }

    public PackIconKind StatusIcon => Status switch
    {
        "Success" => PackIconKind.CheckCircleOutline,
        "Warning" => PackIconKind.AlertCircleOutline,
        "Error" => PackIconKind.CloseCircleOutline,
        _ => PackIconKind.InformationOutline
    };

    public string StatusForeground => Status switch
    {
        "Success" => TidyUp.Styles.ThemeTokens.Success,
        "Warning" => TidyUp.Styles.ThemeTokens.Warning,
        "Error" => TidyUp.Styles.ThemeTokens.Error,
        _ => TidyUp.Styles.ThemeTokens.TextSecondary
    };

    private static string FormatRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.UtcNow - timestamp;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return timestamp.ToLocalTime().ToString("MMM d, HH:mm");
    }
}

/// <summary>
/// ViewModel for the main operational Dashboard view.
/// Displays monitored folders status, operational activity statistics, and recent log feed.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IActionLogRepository _actionLogRepository;
    private readonly IFileMonitorService _fileMonitorService;
    private readonly IWatcherReconciler? _watcherReconciler;
    private readonly IWatcherHealthMonitor? _watcherHealthMonitor;
    private readonly IMonitoredFolderCatalog? _monitoredFolderCatalog;
    private readonly IFolderPathNormalizer _pathNormalizer;

    public event Action<NavigationView>? RequestNavigate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _totalFilesProcessed;

    [ObservableProperty]
    private int _totalSuccess;

    [ObservableProperty]
    private int _totalWarnings;

    [ObservableProperty]
    private int _totalErrors;

    [ObservableProperty]
    private string _successRateFormatted = "100%";

    [ObservableProperty]
    private int _activeRuleCount;

    [ObservableProperty]
    private int _totalRuleCount;

    [ObservableProperty]
    private int _monitoredFolderCount;

    [ObservableProperty]
    private string _lastActivityFormatted = "Never";

    public ObservableCollection<MonitoredFolderItem> MonitoredFolders { get; } = new();
    public ObservableCollection<RecentActivityItem> RecentActivity { get; } = new();

    public DashboardViewModel(
        IRuleRepository ruleRepository,
        IActionLogRepository actionLogRepository,
        IFileMonitorService fileMonitorService,
        IWatcherReconciler? watcherReconciler = null,
        IWatcherHealthMonitor? watcherHealthMonitor = null,
        IMonitoredFolderCatalog? monitoredFolderCatalog = null,
        IFolderPathNormalizer? pathNormalizer = null)
    {
        _ruleRepository = ruleRepository;
        _actionLogRepository = actionLogRepository;
        _fileMonitorService = fileMonitorService;
        _watcherReconciler = watcherReconciler;
        _watcherHealthMonitor = watcherHealthMonitor;
        _monitoredFolderCatalog = monitoredFolderCatalog;
        _pathNormalizer = pathNormalizer ?? new FolderPathNormalizer();

        if (_monitoredFolderCatalog != null)
        {
            _monitoredFolderCatalog.CatalogChanged += OnCatalogChanged;
        }
        else if (_watcherHealthMonitor != null)
        {
            _watcherHealthMonitor.FolderHealthChanged += OnFolderHealthChanged;
        }
    }

    private void OnCatalogChanged(object? sender, FolderCatalogChangedEventArgs e)
    {
        void Update()
        {
            SyncFoldersFromCatalog(e.Folders);
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(Update);
        }
        else
        {
            Update();
        }
    }

    private void SyncFoldersFromCatalog(IEnumerable<MonitoredFolderState> catalogFolders)
    {
        MonitoredFolders.Clear();
        foreach (var state in catalogFolders)
        {
            var folderItem = new MonitoredFolderItem
            {
                Path = state.NormalizedPath,
                IncludeSubfolders = state.IncludeSubfolders,
                RuleCount = state.TotalRuleCount,
                AssociatedRules = state.AssociatedRulesSummary,
                ExclusionPatterns = state.ExclusionPatterns.ToList(),
                ValidationStatus = state.ValidationStatus,
                IsSystemProtected = state.IsSystemProtected
            };

            folderItem.UpdateHealth(state.WatcherHealth, state.StatusDetail);
            MonitoredFolders.Add(folderItem);
        }

        MonitoredFolderCount = MonitoredFolders.Count;
    }

    private void OnFolderHealthChanged(object? sender, FolderHealthReport e)
    {
        void Update()
        {
            var normalized = _pathNormalizer.TryNormalize(e.Path, out var norm, out _) ? norm : e.Path;
            var match = MonitoredFolders.FirstOrDefault(f => string.Equals(f.Path, normalized, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                match.UpdateHealth(e);
            }
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(Update);
        }
        else
        {
            Update();
        }
    }

    /// <summary>
    /// Asynchronously refreshes all statistics, monitored folder cards, and recent logs from the database.
    /// </summary>
    [RelayCommand]
    public async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading dashboard metrics...";

        try
        {
            // 1. Fetch rules off UI thread
            var rules = await _ruleRepository.GetAllAsync();
            TotalRuleCount = rules.Count;
            ActiveRuleCount = rules.Count(r => r.IsEnabled);

            // 2. Fetch log stats
            var stats = await _actionLogRepository.GetStatisticsAsync();
            TotalFilesProcessed = stats.Total;
            TotalSuccess = stats.Success;
            TotalWarnings = stats.Warning;
            TotalErrors = stats.Error;

            if (stats.Total > 0)
            {
                var rate = (double)stats.Success / stats.Total * 100.0;
                SuccessRateFormatted = $"{rate:F1}%";
            }
            else
            {
                SuccessRateFormatted = "100%";
            }

            // 3. Synchronize monitored folders from single-source-of-truth catalog (or normalized fallback)
            if (_monitoredFolderCatalog != null)
            {
                await _monitoredFolderCatalog.RefreshAsync();
                SyncFoldersFromCatalog(_monitoredFolderCatalog.Folders);
            }
            else
            {
                var folderDict = new Dictionary<string, (bool Subfolders, List<string> Rules, HashSet<string> Exclusions, int EnabledCount)>(StringComparer.OrdinalIgnoreCase);

                foreach (var rule in rules)
                {
                    foreach (var folder in rule.MonitoredFolders)
                    {
                        if (string.IsNullOrWhiteSpace(folder.Path))
                            continue;

                        var canonicalPath = _pathNormalizer.TryNormalize(folder.Path, out var norm, out _)
                            ? norm
                            : folder.Path.Trim();

                        if (!folderDict.TryGetValue(canonicalPath, out var item))
                        {
                            item = (false, new List<string>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
                            folderDict[canonicalPath] = item;
                        }

                        if (folder.IncludeSubfolders)
                        {
                            item.Subfolders = true;
                        }

                        if (!item.Rules.Contains(rule.Name))
                        {
                            item.Rules.Add(rule.Name);
                        }

                        if (rule.IsEnabled)
                        {
                            item.EnabledCount++;
                        }

                        foreach (var excl in folder.ExclusionPatterns)
                        {
                            if (!string.IsNullOrWhiteSpace(excl))
                            {
                                item.Exclusions.Add(excl.Trim());
                            }
                        }

                        folderDict[canonicalPath] = item;
                    }
                }

                MonitoredFolders.Clear();
                foreach (var kvp in folderDict.OrderBy(k => k.Key))
                {
                    var folderItem = new MonitoredFolderItem
                    {
                        Path = kvp.Key,
                        IncludeSubfolders = kvp.Value.Subfolders,
                        RuleCount = kvp.Value.Rules.Count,
                        AssociatedRules = string.Join(", ", kvp.Value.Rules),
                        ExclusionPatterns = kvp.Value.Exclusions.OrderBy(x => x).ToList()
                    };

                    if (Directory.Exists(kvp.Key))
                    {
                        _watcherHealthMonitor?.RegisterFolder(kvp.Key, isPaused: kvp.Value.EnabledCount == 0);
                        var health = _watcherHealthMonitor?.GetFolderHealth(kvp.Key);
                        if (health != null)
                        {
                            folderItem.UpdateHealth(health);
                        }
                        else if (kvp.Value.EnabledCount == 0)
                        {
                            folderItem.UpdateHealth(WatcherHealthStatus.Paused, "Monitoring paused (all linked rules disabled)");
                        }
                    }
                    else
                    {
                        _watcherHealthMonitor?.UnregisterFolder(kvp.Key);
                        folderItem.UpdateHealth(WatcherHealthStatus.Disconnected, "Directory does not exist or share is unreachable");
                    }

                    MonitoredFolders.Add(folderItem);
                }

                MonitoredFolderCount = MonitoredFolders.Count;
            }

            // 4. Fetch recent 20 activity records
            var recentLogs = await _actionLogRepository.GetFilteredAsync(maxResults: 20);
            RecentActivity.Clear();

            foreach (var log in recentLogs)
            {
                RecentActivity.Add(new RecentActivityItem
                {
                    Id = log.Id,
                    Timestamp = log.Timestamp,
                    RuleName = log.RuleName,
                    FilePath = log.FilePath,
                    ActionPerformed = log.ActionPerformed,
                    ResultPath = log.ResultPath,
                    Status = log.Status,
                    ErrorMessage = log.ErrorMessage
                });
            }

            if (RecentActivity.Count > 0)
            {
                LastActivityFormatted = RecentActivity[0].RelativeTime;
            }
            else
            {
                LastActivityFormatted = "Never";
            }

            StatusMessage = "Dashboard up to date";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Scans a specific monitored folder for files to organize immediately.
    /// </summary>
    [RelayCommand]
    public async Task ScanFolderAsync(MonitoredFolderItem? folder)
    {
        if (folder is null || !folder.Exists || !folder.CanScan)
            return;

        folder.IsScanning = true;
        StatusMessage = $"Scanning '{folder.DisplayName}'...";

        try
        {
            if (_watcherReconciler != null)
            {
                await _watcherReconciler.ReconcileFolderAsync(
                    folder.Path,
                    folder.IncludeSubfolders,
                    folder.ExclusionPatterns);
            }
            else
            {
                await _fileMonitorService.ScanAllFoldersAsync();
            }

            StatusMessage = $"Finished scan of '{folder.DisplayName}'";
            await LoadDashboardDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            folder.IsScanning = false;
        }
    }

    /// <summary>
    /// Scans all monitored folders for existing files.
    /// </summary>
    [RelayCommand]
    public async Task ScanAllFoldersAsync()
    {
        IsLoading = true;
        StatusMessage = "Scanning all monitored folders...";

        try
        {
            await _fileMonitorService.ScanAllFoldersAsync();
            StatusMessage = "Scan completed";
            await LoadDashboardDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan all error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NavigateToRuleEditor()
    {
        RequestNavigate?.Invoke(NavigationView.RuleEditor);
    }

    [RelayCommand]
    private void NavigateToLogs()
    {
        RequestNavigate?.Invoke(NavigationView.Logs);
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        RequestNavigate?.Invoke(NavigationView.Settings);
    }
}

