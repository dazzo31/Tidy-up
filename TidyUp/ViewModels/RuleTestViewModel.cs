using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TidyUp.Models.Domain;
using TidyUp.Services;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for testing rules against files.
/// </summary>
public partial class RuleTestViewModel : ObservableObject
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IVariableEngine _variableEngine;

    [ObservableProperty]
    private Rule? _rule;

    [ObservableProperty]
    private string _testFolderPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileTestResult> _matchingFiles = new();

    [ObservableProperty]
    private int _totalFilesScanned;

    [ObservableProperty]
    private bool _isTesting;

    public RuleTestViewModel(IRuleEngine ruleEngine, IVariableEngine variableEngine)
    {
        _ruleEngine = ruleEngine;
        _variableEngine = variableEngine;
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder to Test"
        };

        if (dialog.ShowDialog() == true)
        {
            TestFolderPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task TestRuleAsync()
    {
        if (Rule == null || string.IsNullOrEmpty(TestFolderPath) || !Directory.Exists(TestFolderPath))
            return;

        IsTesting = true;
        MatchingFiles.Clear();
        TotalFilesScanned = 0;

        try
        {
            await Task.Run(() =>
            {
                var files = Directory.GetFiles(TestFolderPath, "*.*", SearchOption.AllDirectories);
                TotalFilesScanned = files.Length;

                foreach (var filePath in files)
                {
                    var fileInfo = new FileInfo(filePath);
                    var matches = _ruleEngine.EvaluateRule(Rule, fileInfo);

                    if (matches)
                    {
                        var result = new FileTestResult
                        {
                            FilePath = filePath,
                            FileName = fileInfo.Name,
                            Size = fileInfo.Length,
                            Matches = true
                        };

                        // Show what the actions would produce
                        if (Rule.Actions.Any())
                        {
                            var firstAction = Rule.Actions.First();
                            if (firstAction is RenameFileAction renameAction)
                            {
                                result.PreviewResult = _variableEngine.Resolve(renameAction.NamePattern, fileInfo);
                            }
                            else if (firstAction is MoveFileAction moveAction)
                            {
                                result.PreviewResult = _variableEngine.Resolve(moveAction.DestinationPath, fileInfo);
                            }
                        }

                        App.Current.Dispatcher.Invoke(() => MatchingFiles.Add(result));
                    }
                }
            });
        }
        finally
        {
            IsTesting = false;
        }
    }
}

public class FileTestResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool Matches { get; set; }
    public string? PreviewResult { get; set; }
}
