using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Models.Domain;
using TidyUp.Services;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for the rule preview/test window.
/// </summary>
public partial class RulePreviewViewModel : ObservableObject
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IActionExecutor _actionExecutor;

    [ObservableProperty]
    private ObservableCollection<RulePreviewResult> _previewResults = new();

    [ObservableProperty]
    private RulePreviewResult? _selectedResult;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Click 'Run Preview' to test the rule";

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _matchingFiles;

    [ObservableProperty]
    private int _filesWithWarnings;

    private Rule? _rule;

    public RulePreviewViewModel(IRuleEngine ruleEngine, IActionExecutor actionExecutor)
    {
        _ruleEngine = ruleEngine;
        _actionExecutor = actionExecutor;
    }

    /// <summary>
    /// Sets the rule to preview.
    /// </summary>
    public void SetRule(Rule rule)
    {
        _rule = rule;
        StatusMessage = $"Ready to preview rule: {rule.Name}";
    }

    /// <summary>
    /// Runs the preview/dry run.
    /// </summary>
    [RelayCommand]
    private async Task RunPreviewAsync()
    {
        if (_rule == null || _rule.MonitoredFolders.Count == 0)
        {
            StatusMessage = "No monitored folders configured";
            return;
        }

        IsLoading = true;
        StatusMessage = "Scanning files and running preview...";
        PreviewResults.Clear();

        try
        {
            var allFiles = new List<FileInfo>();

            // Scan all monitored folders
            foreach (var folder in _rule.MonitoredFolders)
            {
                if (!Directory.Exists(folder.Path))
                {
                    StatusMessage = $"Warning: Folder not found: {folder.Path}";
                    continue;
                }

                var searchOption = folder.IncludeSubfolders 
                    ? SearchOption.AllDirectories 
                    : SearchOption.TopDirectoryOnly;

                var files = Directory.GetFiles(folder.Path, "*.*", searchOption)
                    .Select(f => new FileInfo(f))
                    .Where(f => !IsExcluded(f.FullName, folder.ExclusionPatterns.ToList()))
                    .ToList();

                allFiles.AddRange(files);
            }

            TotalFiles = allFiles.Count;
            MatchingFiles = 0;
            FilesWithWarnings = 0;

            // Process each file
            int counter = 0;
            foreach (var file in allFiles.Take(100)) // Limit to 100 files for preview
            {
                counter++;
                var result = await PreviewFileAsync(file, counter);
                PreviewResults.Add(result);

                if (result.Matches)
                    MatchingFiles++;

                if (result.HasWarning)
                    FilesWithWarnings++;
            }

            if (allFiles.Count > 100)
            {
                StatusMessage = $"Preview complete: Showing first 100 of {TotalFiles} files. {MatchingFiles} would match.";
            }
            else
            {
                StatusMessage = $"Preview complete: {MatchingFiles} of {TotalFiles} files would match.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during preview: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<RulePreviewResult> PreviewFileAsync(FileInfo file, int counter)
    {
        var result = new RulePreviewResult
        {
            OriginalPath = file.FullName
        };

        try
        {
            // Check if file matches conditions
            result.Matches = _ruleEngine.EvaluateRule(_rule!, file);

            if (result.Matches)
            {
                // Preview what actions would do
                result.Actions = await _actionExecutor.PreviewActionsAsync(
                    _rule!.Actions.ToList(), 
                    file, 
                    counter);

                // Check for warnings
                result.HasWarning = result.Actions.Any(a => a.HasConflict);
                if (result.HasWarning)
                {
                    var conflictActions = result.Actions.Where(a => a.HasConflict).ToList();
                    result.WarningMessage = $"{conflictActions.Count} action(s) would have conflicts";
                }
            }
        }
        catch (Exception ex)
        {
            result.HasWarning = true;
            result.WarningMessage = $"Error: {ex.Message}";
        }

        return result;
    }

    private bool IsExcluded(string filePath, List<string> exclusionPatterns)
    {
        if (exclusionPatterns == null || !exclusionPatterns.Any())
            return false;

        var fileName = Path.GetFileName(filePath);
        var directory = Path.GetDirectoryName(filePath);

        foreach (var pattern in exclusionPatterns)
        {
            // Check if filename matches pattern
            if (fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check if any parent directory matches pattern
            if (directory != null && directory.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
