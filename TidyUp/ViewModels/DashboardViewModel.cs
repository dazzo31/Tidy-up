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
using TidyUp.Services;
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
        set => SetProperty(ref _isScanning, value);
    }

    private WatcherHealthStatus _healthStatus = WatcherHealthStatus.Healthy;
    public WatcherHealthStatus HealthStatus
    {
        get => _healthStatus;
        set
        {
            if (SetProperty(ref _healthStatus, value))
            {
                OnPropertyChanged(nameof(HealthStatusText));
                OnPropertyChanged(nameof(HealthTooltip));
                OnPropertyChanged(nameof(HealthBadgeBackground));
                OnPropertyChanged(nameof(HealthBadgeForeground));
            }
        }
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

    public string HealthStatusText => HealthStatus switch
    {
        WatcherHealthStatus.Healthy => "Healthy",
        WatcherHealthStatus.Paused => "Paused",
        WatcherHealthStatus.Degraded => "Degraded",
        WatcherHealthStatus.Disconnected => "Disconnected",
        WatcherHealthStatus.Error => "Error",
        _ => "Unknown"
    };

    public string HealthTooltip => !string.IsNullOrWhiteSpace(HealthStatusDetail)
        ? $"{HealthStatusText}: {HealthStatusDetail}"
        : HealthStatus switch
        {
            WatcherHealthStatus.Healthy => "Folder is accessible, operational, and actively watched.",
            WatcherHealthStatus.Paused => "Folder monitoring is currently paused.",
            WatcherHealthStatus.Degraded => "Watcher internal buffer experienced overflow; automatic reconciliation triggered.",
            WatcherHealthStatus.Disconnected => "Folder is unreachable, removed, or network share is disconnected.",
            WatcherHealthStatus.Error => "Folder encountered permission or I/O failure.",
            _ => "Unknown health status"
        };

    public Brush HealthBadgeBackground => HealthStatus switch
    {
        WatcherHealthStatus.Healthy => new SolidColorBrush(Color.FromRgb(232, 245, 233)),      // #E8F5E9
        WatcherHealthStatus.Paused => new SolidColorBrush(Color.FromRgb(245, 245, 245)),       // #F5F5F5
        WatcherHealthStatus.Degraded => new SolidColorBrush(Color.FromRgb(255, 243, 224)),     // #FFF3E0
        WatcherHealthStatus.Disconnected => new SolidColorBrush(Color.FromRgb(255, 235, 238)), // #FFEBEE
        WatcherHealthStatus.Error => new SolidColorBrush(Color.FromRgb(255, 205, 210)),        // #FFCDD2
        _ => new SolidColorBrush(Colors.LightGray)
    };

    public Brush HealthBadgeForeground => HealthStatus switch
    {
        WatcherHealthStatus.Healthy => new SolidColorBrush(Color.FromRgb(46, 125, 50)),       // #2E7D32
        WatcherHealthStatus.Paused => new SolidColorBrush(Color.FromRgb(117, 117, 117)),      // #757575
        WatcherHealthStatus.Degraded => new SolidColorBrush(Color.FromRgb(230, 81, 0)),       // #E65100
        WatcherHealthStatus.Disconnected => new SolidColorBrush(Color.FromRgb(198, 40, 40)),   // #C62828
        WatcherHealthStatus.Error => new SolidColorBrush(Color.FromRgb(183, 28, 28)),          // #B71C1C
        _ => new SolidColorBrush(Colors.Black)
    };

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

    public Brush StatusColor => Status switch
    {
        "Success" => new SolidColorBrush(Color.FromRgb(46, 125, 50)),   // Green
        "Warning" => new SolidColorBrush(Color.FromRgb(245, 124, 0)),  // Orange
        "Error" => new SolidColorBrush(Color.FromRgb(211, 47, 47)),    // Red
        _ => new SolidColorBrush(Colors.Gray)
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
        IWatcherHealthMonitor? watcherHealthMonitor = null)
    {
        _ruleRepository = ruleRepository;
        _actionLogRepository = actionLogRepository;
        _fileMonitorService = fileMonitorService;
        _watcherReconciler = watcherReconciler;
        _watcherHealthMonitor = watcherHealthMonitor;

        if (_watcherHealthMonitor != null)
        {
            _watcherHealthMonitor.FolderHealthChanged += OnFolderHealthChanged;
        }
    }

    private void OnFolderHealthChanged(object? sender, FolderHealthReport e)
    {
        void Update()
        {
            var match = MonitoredFolders.FirstOrDefault(f => string.Equals(f.Path, e.Path, StringComparison.OrdinalIgnoreCase));
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

            // 3. Extract distinct monitored folders across enabled rules
            var folderDict = new Dictionary<string, (bool Subfolders, List<string> Rules, List<string> Exclusions)>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in rules)
            {
                foreach (var folder in rule.MonitoredFolders)
                {
                    if (string.IsNullOrWhiteSpace(folder.Path))
                        continue;

                    if (!folderDict.TryGetValue(folder.Path, out var item))
                    {
                        item = (folder.IncludeSubfolders, new List<string>(), folder.ExclusionPatterns.ToList());
                        folderDict[folder.Path] = item;
                    }

                    if (!item.Rules.Contains(rule.Name))
                    {
                        item.Rules.Add(rule.Name);
                    }
                }
            }

            MonitoredFolders.Clear();
            foreach (var kvp in folderDict)
            {
                var folderItem = new MonitoredFolderItem
                {
                    Path = kvp.Key,
                    IncludeSubfolders = kvp.Value.Subfolders,
                    RuleCount = kvp.Value.Rules.Count,
                    AssociatedRules = string.Join(", ", kvp.Value.Rules),
                    ExclusionPatterns = kvp.Value.Exclusions
                };

                _watcherHealthMonitor?.RegisterFolder(kvp.Key);
                var health = _watcherHealthMonitor?.GetFolderHealth(kvp.Key);
                if (health != null)
                {
                    folderItem.UpdateHealth(health);
                }
                else if (!Directory.Exists(kvp.Key))
                {
                    folderItem.UpdateHealth(WatcherHealthStatus.Disconnected, "Directory does not exist or share is unreachable");
                }

                MonitoredFolders.Add(folderItem);
            }
            MonitoredFolderCount = MonitoredFolders.Count;

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
        if (folder is null || !folder.Exists)
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

