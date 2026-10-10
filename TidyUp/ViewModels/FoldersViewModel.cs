using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Services.Monitoring;
using TidyUp.Services.Watcher;

namespace TidyUp.ViewModels;

/// <summary>
/// Representation of an aggregated monitored directory across rules.
/// </summary>
public partial class MonitoredFolderItemViewModel : ObservableObject
{
    [ObservableProperty]
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
    private bool _includeSubfolders = true;

    [ObservableProperty]
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

    public string RecursiveDisplay => IncludeSubfolders ? "Recursive" : "Top-level only";

    public bool IsHealthy => Exists && HasReadPermission && HasWritePermission && !IsSystemProtected;

    public string StatusBadgeColor => !Exists ? "#D32F2F" : (IsSystemProtected ? "#F57C00" : (IsHealthy ? "#388E3C" : "#FFA000"));

    public string StatusBadgeText => !Exists ? "Missing" : (IsSystemProtected ? "Protected System Path" : (IsHealthy ? "Accessible" : "Restricted"));
}

/// <summary>
/// ViewModel for the centralized Folders Management workspace.
/// </summary>
public partial class FoldersViewModel : ObservableObject
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IWatcherHealthMonitor? _healthMonitor;

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
    private string _newFolderPathInput = string.Empty;

    [ObservableProperty]
    private bool _newFolderIncludeSubfolders = true;

    [ObservableProperty]
    private string _newFolderExclusionsInput = string.Empty;

    public Func<string?>? FolderPickerCallback { get; set; }
    public Action<Guid>? RequestEditRuleCallback { get; set; }

    public FoldersViewModel(
        IRuleRepository ruleRepository,
        IWatcherHealthMonitor? healthMonitor = null)
    {
        _ruleRepository = ruleRepository;
        _healthMonitor = healthMonitor;
    }

    [RelayCommand]
    public async Task LoadFoldersAsync()
    {
        IsLoading = true;
        StatusMessage = "Analyzing monitored folders...";

        try
        {
            var rules = await _ruleRepository.GetAllAsync();
            var folderMap = new Dictionary<string, MonitoredFolderItemViewModel>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in rules)
            {
                foreach (var folder in rule.MonitoredFolders)
                {
                    if (string.IsNullOrWhiteSpace(folder.Path)) continue;

                    var normalizedPath = folder.Path.Trim();
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

            MonitoredFolders.Clear();
            foreach (var item in folderMap.Values.OrderBy(f => f.Path))
            {
                MonitoredFolders.Add(item);
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
        }
        catch (Exception ex)
        {
            item.Exists = false;
            item.PermissionStatus = $"Inspection failed: {ex.Message}";
            item.WatcherStatus = "Error";
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

    [RelayCommand]
    public void AddMonitoredFolder()
    {
        if (string.IsNullOrWhiteSpace(NewFolderPathInput))
        {
            StatusMessage = "Please enter or select a folder path.";
            return;
        }

        var path = NewFolderPathInput.Trim();
        if (MonitoredFolders.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Folder '{path}' is already being monitored.";
            return;
        }

        var exclusions = (NewFolderExclusionsInput ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var item = new MonitoredFolderItemViewModel
        {
            Path = path,
            IncludeSubfolders = NewFolderIncludeSubfolders,
            ExclusionPatternsText = string.Join(", ", exclusions),
            ExclusionPatterns = new ObservableCollection<string>(exclusions),
            LinkedRulesCount = 0,
            LinkedRulesSummary = "No rules linked yet"
        };

        InspectPath(item);
        MonitoredFolders.Add(item);
        UpdateStats();
        FilterFolders();

        NewFolderPathInput = string.Empty;
        NewFolderExclusionsInput = string.Empty;
        StatusMessage = $"Added monitored directory: {path}";
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

        StatusMessage = $"Scanning '{folder.Path}'...";
        try
        {
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
    }

    [RelayCommand]
    public void TogglePauseWatcher(MonitoredFolderItemViewModel? folder)
    {
        if (folder == null) return;
        folder.IsPaused = !folder.IsPaused;
        folder.WatcherStatus = folder.IsPaused ? "Paused" : (folder.IsHealthy ? "Watching" : "Degraded");
        StatusMessage = $"Watcher for '{folder.Path}' is now {folder.WatcherStatus}.";
    }

    [RelayCommand]
    public void EditLinkedRules(MonitoredFolderItemViewModel? folder)
    {
        if (folder == null || folder.LinkedRuleIds.Count == 0) return;
        RequestEditRuleCallback?.Invoke(folder.LinkedRuleIds[0]);
    }
}
