using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Models.ViewModels;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for editing condition trees.
/// </summary>
public partial class ConditionEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private ConditionGroup? _rootCondition;

    [ObservableProperty]
    private object? _selectedItem;

    [ObservableProperty]
    private ObservableCollection<PreviewFileItem> _previewFiles = new();

    [ObservableProperty]
    private bool _isLoadingPreview;

    [ObservableProperty]
    private int _matchingFilesCount;

    [ObservableProperty]
    private bool _hasMonitoredFolders;

    private List<MonitoredFolder> _monitoredFolders = new();
    private CancellationTokenSource? _previewCts;
    private readonly HashSet<Condition> _wiredConditions = new();

    public ConditionEditorViewModel()
    {
        // Initialize with empty root group
        RootCondition = new ConditionGroup { Operator = LogicOperator.And };
    }

    /// <summary>
    /// Adds a new condition to the selected group.
    /// </summary>
    [RelayCommand]
    private void AddCondition(ConditionGroup? targetGroup = null)
    {
        var group = targetGroup ?? RootCondition;
        if (group == null) return;

        // Add a default file name condition
        var newCondition = new FileNameCondition
        {
            Operator = StringOperator.Contains,
            Value = ""
        };

        group.Conditions.Add(newCondition);
        SelectedItem = newCondition;
        
        // Refresh preview
        _ = RefreshPreviewAsync();
    }

    /// <summary>
    /// Adds a new condition group (AND/OR).
    /// </summary>
    [RelayCommand]
    private void AddGroup(ConditionGroup? targetGroup = null)
    {
        var group = targetGroup ?? RootCondition;
        if (group == null) return;

        var newGroup = new ConditionGroup
        {
            Operator = LogicOperator.And
        };

        group.Conditions.Add(newGroup);
        SelectedItem = newGroup;
        
        // Refresh preview
        _ = RefreshPreviewAsync();
    }

    /// <summary>
    /// Removes the selected condition or group.
    /// </summary>
    [RelayCommand]
    private void RemoveCondition(Condition? condition)
    {
        if (condition == null || RootCondition == null) return;

        RemoveConditionRecursive(RootCondition, condition);
        
        // Refresh preview
        _ = RefreshPreviewAsync();
    }

    private bool RemoveConditionRecursive(ConditionGroup group, Condition target)
    {
        if (group.Conditions.Remove(target))
            return true;

        foreach (var condition in group.Conditions.OfType<ConditionGroup>())
        {
            if (RemoveConditionRecursive(condition, target))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Changes the condition type (FileName, Extension, Size, Date).
    /// </summary>
    [RelayCommand]
    private void ChangeConditionType(string conditionType)
    {
        if (SelectedItem is not Condition selectedCondition) return;
        if (RootCondition == null) return;

        Condition newCondition = conditionType switch
        {
            "FileName" => new FileNameCondition { Operator = StringOperator.Contains, Value = "" },
            "Extension" => new FileExtensionCondition { Operator = StringOperator.Is, Value = "" },
            "Size" => new FileSizeCondition { Operator = FileSizeCondition.SizeOperator.GreaterThan, Value = 0 },
            "Date" => new FileDateCondition 
            { 
                Type = FileDateCondition.DateType.Modified,
                Operator = FileDateCondition.DateOperator.After,
                Value = DateTime.Today
            },
            _ => selectedCondition
        };

        ReplaceConditionRecursive(RootCondition, selectedCondition, newCondition);
        SelectedItem = newCondition;
    }

    private bool ReplaceConditionRecursive(ConditionGroup group, Condition oldCondition, Condition newCondition)
    {
        var index = group.Conditions.IndexOf(oldCondition);
        if (index >= 0)
        {
            group.Conditions[index] = newCondition;
            return true;
        }

        foreach (var condition in group.Conditions.OfType<ConditionGroup>())
        {
            if (ReplaceConditionRecursive(condition, oldCondition, newCondition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the display name for a condition type.
    /// </summary>
    public static string GetConditionTypeName(Condition condition)
    {
        return condition switch
        {
            FileNameCondition => "File Name",
            FileExtensionCondition => "Extension",
            FileSizeCondition => "File Size",
            FileDateCondition => "Date",
            ConditionGroup => "Group",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Sets the monitored folders to scan for preview.
    /// </summary>
    public void SetMonitoredFolders(List<MonitoredFolder> folders)
    {
        _monitoredFolders = folders ?? new List<MonitoredFolder>();
        HasMonitoredFolders = _monitoredFolders.Any();
        _ = RefreshPreviewAsync();
    }

    /// <summary>
    /// Refreshes the live preview by scanning monitored folders.
    /// </summary>
    [RelayCommand]
    private async Task RefreshPreviewAsync()
    {
        // Cancel any existing preview operation
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;

        if (!HasMonitoredFolders || RootCondition == null)
        {
            PreviewFiles.Clear();
            MatchingFilesCount = 0;
            return;
        }

        IsLoadingPreview = true;
        PreviewFiles.Clear();

        try
        {
            await Task.Run(() =>
            {
                var previewItems = new List<PreviewFileItem>();

                foreach (var folder in _monitoredFolders)
                {
                    if (token.IsCancellationRequested) break;

                    if (!Directory.Exists(folder.Path)) continue;

                    try
                    {
                        var searchOption = folder.IncludeSubfolders
                            ? SearchOption.AllDirectories
                            : SearchOption.TopDirectoryOnly;

                        var files = Directory.GetFiles(folder.Path, "*.*", searchOption);

                        // Limit to first 500 files for performance
                        foreach (var filePath in files.Take(500))
                        {
                            if (token.IsCancellationRequested) break;

                            try
                            {
                                // Check exclusion patterns
                                if (folder.ExclusionPatterns.Any(pattern =>
                                    filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                                {
                                    continue;
                                }

                                var fileInfo = new FileInfo(filePath);
                                if (!fileInfo.Exists) continue;

                                // Evaluate conditions
                                bool matches = RootCondition.Evaluate(fileInfo);

                                previewItems.Add(new PreviewFileItem
                                {
                                    FileName = fileInfo.Name,
                                    FolderPath = fileInfo.DirectoryName ?? string.Empty,
                                    FileSize = fileInfo.Length,
                                    ModifiedDate = fileInfo.LastWriteTime,
                                    Matches = matches
                                });
                            }
                            catch
                            {
                                // Skip files that can't be accessed
                            }
                        }
                    }
                    catch
                    {
                        // Skip folders that can't be accessed
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    // Update UI on main thread
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        PreviewFiles.Clear();
                        foreach (var item in previewItems.OrderByDescending(x => x.Matches))
                        {
                            PreviewFiles.Add(item);
                        }
                        MatchingFilesCount = previewItems.Count(x => x.Matches);
                    });
                }
            }, token);
        }
        catch (OperationCanceledException)
        {
            // Preview was cancelled, ignore
        }
        catch (Exception)
        {
            // Handle errors silently for now
            PreviewFiles.Clear();
            MatchingFilesCount = 0;
        }
        finally
        {
            IsLoadingPreview = false;
        }
    }

    /// <summary>
    /// Wire up PropertyChanged events for a condition and its children.
    /// </summary>
    private void WireCondition(Condition condition)
    {
        if (condition == null || _wiredConditions.Contains(condition))
            return;

        _wiredConditions.Add(condition);
        condition.PropertyChanged += Condition_PropertyChanged;

        if (condition is ConditionGroup group)
        {
            WireConditionGroup(group);
        }
    }

    /// <summary>
    /// Unwire PropertyChanged events for a condition and its children.
    /// </summary>
    private void UnwireCondition(Condition condition)
    {
        if (condition == null || !_wiredConditions.Contains(condition))
            return;

        _wiredConditions.Remove(condition);
        condition.PropertyChanged -= Condition_PropertyChanged;

        if (condition is ConditionGroup group)
        {
            UnwireConditionGroup(group);
        }
    }

    /// <summary>
    /// Wire up CollectionChanged events for a condition group.
    /// </summary>
    private void WireConditionGroup(ConditionGroup group)
    {
        if (group?.Conditions == null)
            return;

        group.Conditions.CollectionChanged += Conditions_CollectionChanged;

        foreach (var condition in group.Conditions)
        {
            WireCondition(condition);
        }
    }

    /// <summary>
    /// Unwire CollectionChanged events for a condition group.
    /// </summary>
    private void UnwireConditionGroup(ConditionGroup group)
    {
        if (group?.Conditions == null)
            return;

        group.Conditions.CollectionChanged -= Conditions_CollectionChanged;

        foreach (var condition in group.Conditions)
        {
            UnwireCondition(condition);
        }
    }

    /// <summary>
    /// Event handler for condition property changes - triggers preview refresh.
    /// </summary>
    private void Condition_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = RefreshPreviewAsync();
    }

    /// <summary>
    /// Event handler for condition collection changes - wires/unwires and triggers refresh.
    /// </summary>
    private void Conditions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Unwire old items
        if (e.OldItems != null)
        {
            foreach (Condition condition in e.OldItems)
            {
                UnwireCondition(condition);
            }
        }

        // Wire new items
        if (e.NewItems != null)
        {
            foreach (Condition condition in e.NewItems)
            {
                WireCondition(condition);
            }
        }

        _ = RefreshPreviewAsync();
    }

    /// <summary>
    /// MVVM Toolkit partial method - called before RootCondition changes.
    /// </summary>
    partial void OnRootConditionChanging(ConditionGroup? value)
    {
        if (value != null)
        {
            UnwireConditionGroup(value);
        }
    }

    /// <summary>
    /// MVVM Toolkit partial method - called after RootCondition changes.
    /// </summary>
    partial void OnRootConditionChanged(ConditionGroup? value)
    {
        if (value != null)
        {
            WireConditionGroup(value);
        }
        _ = RefreshPreviewAsync();
    }
}
