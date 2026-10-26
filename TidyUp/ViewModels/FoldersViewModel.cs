using TidyUp.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Models.Folders;
using TidyUp.Services.Folders;
using TidyUp.Services.Monitoring;
using TidyUp.Services.Watcher;

namespace TidyUp.ViewModels;

/// <summary>
/// Representation of an aggregated monitored directory across rules.
/// </summary>
public partial class MonitoredFolderItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    [ObservableProperty]
    private bool _isStandalone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTransientLocation))]
    private string _path = string.Empty;

    [ObservableProperty]
    private bool _exists;

    [ObservableProperty]
    private bool _hasReadPermission;

    [ObservableProperty]
    private bool _hasWritePermission;

    [ObservableProperty]
    private string _permissionStatus = "Checking...";

    [ObservableProperty]
    private bool _isSystemProtected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecursiveDisplay))]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExclusionsDisplay))]
    private string _exclusionPatternsText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _exclusionPatterns = new();

    [ObservableProperty]
    private int _linkedRulesCount;

    [ObservableProperty]
    private string _linkedRulesSummary = string.Empty;

    [ObservableProperty]
    private List<Guid> _linkedRuleIds = new();

    [ObservableProperty]
    private string _watcherStatus = "Watching";

    [ObservableProperty]
    private bool _isPaused;

    public string RecursiveDisplay => IncludeSubfolders ? "Includes subfolders" : "Top-level only";

    /// <summary>Exclusions for display: an em dash when there are none, so the cell never looks broken.</summary>
    public string ExclusionsDisplay => string.IsNullOrWhiteSpace(ExclusionPatternsText) ? "—" : ExclusionPatternsText;

    public bool IsTransientLocation => TransientLocations.IsTransient(Path);

    public bool IsHealthy => Exists && HasReadPermission && HasWritePermission && !IsSystemProtected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScan))]
    [NotifyPropertyChangedFor(nameof(ScanTooltip))]
    private bool _isScanning;

    /// <summary>The same status the Dashboard shows for this folder.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScan))]
    [NotifyPropertyChangedFor(nameof(ScanTooltip))]
    private FolderStatus _status = FolderStatus.Healthy;

    public bool CanScan => !IsScanning && FolderStatusPresentation.CanScan(Status);

    public string ScanTooltip => IsScanning ? "Scan in progress" : FolderStatusPresentation.ScanTooltip(Status);

    /// <summary>Recomputes <see cref="Status"/> from the inspected flags (used when there is no catalog state).</summary>
    public void RefreshStatus()
    {
        Status = !Exists ? FolderStatus.Missing
            : IsSystemProtected ? FolderStatus.Protected
            : (!HasReadPermission || !HasWritePermission) ? FolderStatus.AccessDenied
            : IsPaused ? FolderStatus.Paused
            : FolderStatus.Healthy;
    }
}

/// <summary>
/// ViewModel for the centralized Folders Management workspace.
/// </summary>
public partial class FoldersViewModel : ObservableObject
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IWatcherHealthMonitor? _healthMonitor;
    private readonly IMonitoredFolderCatalog? _monitoredFolderCatalog;
    private readonly IFolderPathNormalizer _pathNormalizer;
    private readonly IWatcherReconciler? _watcherReconciler;
    private readonly IFileMonitorService? _fileMonitorService;

    [ObservableProperty]
    private ObservableCollection<MonitoredFolderItemViewModel> _monitoredFolders = new();

    [ObservableProperty]
    private ObservableCollection<MonitoredFolderItemViewModel> _filteredFolders = new();

    [ObservableProperty]
    private MonitoredFolderItemViewModel? _selectedFolder;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _totalFoldersCount;

    [ObservableProperty]
    private int _accessibleCount;

    [ObservableProperty]
    private int _inaccessibleCount;

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterFolders();
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddMonitoredFolderCommand))]
    private string _newFolderPathInput = string.Empty;

    [ObservableProperty]
    private bool _newFolderIncludeSubfolders = true;

    [ObservableProperty]
    private string _newFolderExclusionsInput = string.Empty;

    public Func<string?>? FolderPickerCallback { get; set; }
    public Action<Guid>? RequestEditRuleCallback { get; set; }
    public Action<string>? RequestCreateRuleForFolderCallback { get; set; }

    public FoldersViewModel(
        IRuleRepository ruleRepository,
        IWatcherHealthMonitor? healthMonitor = null,
        IMonitoredFolderCatalog? monitoredFolderCatalog = null,
        IFolderPathNormalizer? pathNormalizer = null,
        IWatcherReconciler? watcherReconciler = null,
        IFileMonitorService? fileMonitorService = null)
    {
        _ruleRepository = ruleRepository;
        _healthMonitor = healthMonitor;
        _monitoredFolderCatalog = monitoredFolderCatalog;
        _pathNormalizer = pathNormalizer ?? new FolderPathNormalizer();
        _watcherReconciler = watcherReconciler;
        _fileMonitorService = fileMonitorService;

        if (_monitoredFolderCatalog != null)
        {
            _monitoredFolderCatalog.CatalogChanged += OnCatalogChanged;
        }
    }

    private void OnCatalogChanged(object? sender, FolderCatalogChangedEventArgs e)
    {
        void Update()
        {
            SyncFoldersFromCatalog(e.Folders);
            UpdateStats();
            FilterFolders();
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

    private void SyncFoldersFromCatalog(IEnumerable<MonitoredFolderState>? catalogFolders)
    {
        MonitoredFolders.Clear();
        if (catalogFolders == null) return;
        foreach (var state in catalogFolders.OrderBy(s => s.NormalizedPath))
        {
            var isPaused = state.WatcherHealth == WatcherHealthStatus.Paused;
            var watcherStatusText = state.ValidationStatus switch
            {
                FolderValidationStatus.Missing => "Error (Missing)",
                FolderValidationStatus.AccessDenied => "Error (Access Denied)",
                _ => state.WatcherHealth switch
                {
                    WatcherHealthStatus.Paused => "Paused",
                    WatcherHealthStatus.Degraded => "Degraded",
                    WatcherHealthStatus.Disconnected => "Error (Missing)",
                    WatcherHealthStatus.Error => "Error",
                    _ => "Watching"
                }
            };

            var permissionStatusText = state.ValidationStatus switch
            {
                FolderValidationStatus.Missing => "Directory does not exist",
                FolderValidationStatus.AccessDenied => "Access Denied",
                FolderValidationStatus.InvalidPath => "Invalid path",
                _ => (state.HasReadPermission, state.HasWritePermission) switch
                {
                    (true, true) => "Read / Write (Full Access)",
                    (true, false) => "Read Only",
                    _ => "Access Denied"
                }
            };

            if (_fileMonitorService != null && _fileMonitorService.IsFolderPaused(state.NormalizedPath))
            {
                isPaused = true;
            }

            var item = new MonitoredFolderItemViewModel
            {
                Id = state.Id,
                IsStandalone = state.IsStandalone,
                Path = state.NormalizedPath,
                Exists = state.Exists,
                HasReadPermission = state.HasReadPermission,
                HasWritePermission = state.HasWritePermission,
                IsSystemProtected = state.IsSystemProtected,
                PermissionStatus = permissionStatusText,
                IncludeSubfolders = state.IncludeSubfolders,
                ExclusionPatternsText = string.Join(", ", state.ExclusionPatterns),
                ExclusionPatterns = new ObservableCollection<string>(state.ExclusionPatterns),
                LinkedRulesCount = state.TotalRuleCount,
                LinkedRuleIds = state.LinkedRuleIds.ToList(),
                LinkedRulesSummary = state.AssociatedRulesSummary,
                WatcherStatus = watcherStatusText,
                IsPaused = isPaused,
                Status = state.Status
            };

            MonitoredFolders.Add(item);
        }
    }

    [RelayCommand]
    public async Task LoadFoldersAsync()
    {
        IsLoading = true;
        StatusMessage = "Analyzing monitored folders...";

        try
        {
            if (_monitoredFolderCatalog != null)
            {
                await _monitoredFolderCatalog.RefreshAsync();
                SyncFoldersFromCatalog(_monitoredFolderCatalog.Folders);
            }
            else
            {
                var rules = await _ruleRepository.GetAllAsync();
                var folderMap = new Dictionary<string, MonitoredFolderItemViewModel>(StringComparer.OrdinalIgnoreCase);

                foreach (var rule in rules)
                {
                    foreach (var folder in rule.MonitoredFolders)
                    {
                        if (string.IsNullOrWhiteSpace(folder.Path)) continue;

                        var normalizedPath = _pathNormalizer.TryNormalize(folder.Path, out var norm, out _)
                            ? norm
                            : folder.Path.Trim();

                        if (!folderMap.TryGetValue(normalizedPath, out var item))
                        {
                            item = new MonitoredFolderItemViewModel
                            {
                                Path = normalizedPath,
                                IncludeSubfolders = folder.IncludeSubfolders,
                                ExclusionPatternsText = string.Join(", ", folder.ExclusionPatterns),
                                ExclusionPatterns = new ObservableCollection<string>(folder.ExclusionPatterns)
                            };

                            InspectPath(item);
                            folderMap[normalizedPath] = item;
                        }
                        else
                        {
                            if (folder.IncludeSubfolders)
                            {
                                item.IncludeSubfolders = true;
                            }

                            foreach (var pattern in folder.ExclusionPatterns)
                            {
                                if (!item.ExclusionPatterns.Any(p => string.Equals(p, pattern, StringComparison.OrdinalIgnoreCase)))
                                {
                                    item.ExclusionPatterns.Add(pattern);
                                }
                            }
                            item.ExclusionPatternsText = string.Join(", ", item.ExclusionPatterns);
                        }

                        if (!item.LinkedRuleIds.Contains(rule.Id))
                        {
                            item.LinkedRulesCount++;
                            item.LinkedRuleIds.Add(rule.Id);
                            if (!string.IsNullOrWhiteSpace(item.LinkedRulesSummary))
                            {
                                item.LinkedRulesSummary += $", {rule.Name}";
                            }
                            else
                            {
                                item.LinkedRulesSummary = rule.Name;
                            }
                        }
                    }
                }

                MonitoredFolders.Clear();
                foreach (var item in folderMap.Values.OrderBy(f => f.Path))
                {
                    MonitoredFolders.Add(item);
                }
            }

            UpdateStats();
            FilterFolders();
            StatusMessage = $"Loaded {MonitoredFolders.Count} monitored directories";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading folders: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public static void InspectPath(MonitoredFolderItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            item.Exists = false;
            item.PermissionStatus = "Empty path";
            item.RefreshStatus();
            return;
        }

        try
        {
            item.IsSystemProtected = IsSystemProtectedDirectory(item.Path);

            if (!Directory.Exists(item.Path))
            {
                item.Exists = false;
                item.HasReadPermission = false;
                item.HasWritePermission = false;
                item.PermissionStatus = "Directory does not exist";
                item.WatcherStatus = "Error (Missing)";
                item.RefreshStatus();
                return;
            }

            item.Exists = true;

            // Test Read
            try
            {
                _ = Directory.EnumerateFileSystemEntries(item.Path).FirstOrDefault();
                item.HasReadPermission = true;
            }
            catch (UnauthorizedAccessException)
            {
                item.HasReadPermission = false;
            }

            // Test Write
            try
            {
                var testFile = Path.Combine(item.Path, $".tidyup_perm_test_{Guid.NewGuid():N}.tmp");
                using (var fs = File.Create(testFile, 1, FileOptions.DeleteOnClose))
                {
                    item.HasWritePermission = true;
                }
            }
            catch
            {
                item.HasWritePermission = false;
            }

            item.PermissionStatus = (item.HasReadPermission, item.HasWritePermission) switch
            {
                (true, true) => "Read / Write (Full Access)",
                (true, false) => "Read Only",
                (false, _) => "Access Denied"
            };

            item.WatcherStatus = item.IsHealthy ? "Watching" : "Degraded";
            item.RefreshStatus();
        }
        catch (Exception ex)
        {
            item.Exists = false;
            item.PermissionStatus = $"Inspection failed: {ex.Message}";
            item.WatcherStatus = "Error";
            item.RefreshStatus();
        }
    }

    public static bool IsSystemProtectedDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path).TrimEnd('\\', '/');
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
            var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
            var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\', '/');
            var sysRoot = Path.GetPathRoot(full)?.TrimEnd('\\', '/');

            if (full.Equals(winDir, StringComparison.OrdinalIgnoreCase) || full.StartsWith(winDir + "\\", StringComparison.OrdinalIgnoreCase))
                return true;
            if (full.Equals(progFiles, StringComparison.OrdinalIgnoreCase) || full.StartsWith(progFiles + "\\", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(progFilesX86) && (full.Equals(progFilesX86, StringComparison.OrdinalIgnoreCase) || full.StartsWith(progFilesX86 + "\\", StringComparison.OrdinalIgnoreCase)))
                return true;
            if (string.Equals(full, sysRoot, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateStats()
    {
        TotalFoldersCount = MonitoredFolders.Count;
        AccessibleCount = MonitoredFolders.Count(f => f.IsHealthy);
        InaccessibleCount = MonitoredFolders.Count(f => !f.IsHealthy);
    }

    private void FilterFolders()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(query)
            ? MonitoredFolders.ToList()
            : MonitoredFolders.Where(f =>
                f.Path.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                f.LinkedRulesSummary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                f.PermissionStatus.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        FilteredFolders.Clear();
        foreach (var item in filtered)
        {
            FilteredFolders.Add(item);
        }
    }

    [RelayCommand]
    public void BrowseFolder()
    {
        string? selected = null;
        if (FolderPickerCallback != null)
        {
            selected = FolderPickerCallback();
        }
        else
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Monitored Directory",
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                selected = dialog.FolderName;
            }
        }

        if (!string.IsNullOrWhiteSpace(selected))
        {
            NewFolderPathInput = selected;
        }
    }

    public bool CanAddMonitoredFolder => !string.IsNullOrWhiteSpace(NewFolderPathInput);

    [RelayCommand(CanExecute = nameof(CanAddMonitoredFolder))]
    public async Task AddMonitoredFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFolderPathInput))
        {
            StatusMessage = "Please enter or select a folder path.";
            return;
        }

        var path = _pathNormalizer.TryNormalize(NewFolderPathInput, out var normalized, out _)
            ? normalized
            : NewFolderPathInput.Trim();
        if (MonitoredFolders.Any(f => _pathNormalizer.AreEqual(f.Path, path)))
        {
            StatusMessage = $"Folder '{path}' is already being monitored.";
            return;
        }

        var exclusions = (NewFolderExclusionsInput ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        try
        {
            if (_monitoredFolderCatalog != null)
            {
                await _monitoredFolderCatalog.AddStandaloneFolderAsync(path, NewFolderIncludeSubfolders, exclusions);
                await _monitoredFolderCatalog.RefreshAsync();
                SyncFoldersFromCatalog(_monitoredFolderCatalog.Folders);
            }
            else
            {
                var item = new MonitoredFolderItemViewModel
                {
                    Path = path,
                    IncludeSubfolders = NewFolderIncludeSubfolders,
                    ExclusionPatternsText = string.Join(", ", exclusions),
                    ExclusionPatterns = new ObservableCollection<string>(exclusions),
                    LinkedRulesCount = 0,
                    LinkedRulesSummary = "No rules linked yet",
                    IsStandalone = true
                };

                InspectPath(item);
                MonitoredFolders.Add(item);
            }

            UpdateStats();
            FilterFolders();

            NewFolderPathInput = string.Empty;
            NewFolderExclusionsInput = string.Empty;
            StatusMessage = $"Added monitored directory: {path}. Create a rule to start organizing files in this folder.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add folder '{path}': {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenInExplorer(MonitoredFolderItemViewModel? folder)
    {
        if (folder == null || string.IsNullOrWhiteSpace(folder.Path)) return;

        try
        {
            if (Directory.Exists(folder.Path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folder.Path}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                StatusMessage = $"Directory does not exist: {folder.Path}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cannot open explorer: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ScanFolderNowAsync(MonitoredFolderItemViewModel? folder)
    {
        if (folder == null || string.IsNullOrWhiteSpace(folder.Path)) return;

        if (!Directory.Exists(folder.Path))
        {
            StatusMessage = $"Cannot scan: folder does not exist ({folder.Path})";
            return;
        }

        if (!folder.CanScan)
        {
            StatusMessage = $"Cannot scan '{folder.Path}': {FolderStatusPresentation.Text(folder.Status).ToLowerInvariant()}.";
            return;
        }

        folder.IsScanning = true;
        StatusMessage = $"Scanning '{folder.Path}'...";
        try
        {
            if (_watcherReconciler != null)
            {
                await _watcherReconciler.ReconcileFolderAsync(
                    folder.Path,
                    folder.IncludeSubfolders,
                    folder.ExclusionPatterns.ToList());
            }

            int count = await Task.Run(() =>
            {
                var searchOption = folder.IncludeSubfolders
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var files = Directory.EnumerateFiles(folder.Path, "*.*", searchOption);
                if (folder.ExclusionPatterns.Count > 0)
                {
                    files = files.Where(filePath =>
                        !folder.ExclusionPatterns.Any(p => filePath.Contains(p, StringComparison.OrdinalIgnoreCase)));
                }
                return files.Count();
            });

            StatusMessage = $"Scan complete: found {count} active files in '{folder.Path}'.";
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

    [RelayCommand]
    public async Task TogglePauseWatcherAsync(MonitoredFolderItemViewModel? folder)
    {
        if (folder == null) return;

        var previousPaused = folder.IsPaused;
        var newPaused = !previousPaused;

        try
        {
            // Optimistic update
            folder.IsPaused = newPaused;
            folder.WatcherStatus = folder.IsPaused ? "Paused" : (folder.IsHealthy ? "Watching" : "Degraded");
            folder.RefreshStatus();

            if (_fileMonitorService != null)
            {
                if (newPaused)
                    _fileMonitorService.PauseFolder(folder.Path);
                else
                    _fileMonitorService.ResumeFolder(folder.Path);
            }

            if (_monitoredFolderCatalog != null)
            {
                await _monitoredFolderCatalog.SetFolderPausedAsync(folder.Path, newPaused);
            }
            else if (_healthMonitor != null)
            {
                _healthMonitor.SetFolderPaused(folder.Path, newPaused);
            }

            StatusMessage = $"Watcher for '{folder.Path}' is now {folder.WatcherStatus}.";
        }
        catch (Exception ex)
        {
            // Revert optimistic update on failure
            folder.IsPaused = previousPaused;
            folder.WatcherStatus = folder.IsPaused ? "Paused" : (folder.IsHealthy ? "Watching" : "Degraded");
            folder.RefreshStatus();
            StatusMessage = $"Failed to change pause state for '{folder.Path}': {ex.Message}";
        }
    }

    [RelayCommand]
    public void EditLinkedRules(MonitoredFolderItemViewModel? folder)
    {
        if (folder == null) return;
        if (folder.LinkedRuleIds.Count > 0)
        {
            RequestEditRuleCallback?.Invoke(folder.LinkedRuleIds[0]);
        }
        else if (RequestCreateRuleForFolderCallback != null)
        {
            RequestCreateRuleForFolderCallback(folder.Path);
        }
        else
        {
            StatusMessage = $"No rules are currently linked to '{folder.Path}'. Create a new rule to monitor this folder.";
        }
    }
}

