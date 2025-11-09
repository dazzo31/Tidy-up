using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for the main window.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IImportExportService _importExportService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private ObservableCollection<Rule> _rules = new();

    private Rule? _selectedRule;
    
    public Rule? SelectedRule
    {
        get => _selectedRule;
        set
        {
            if (SetProperty(ref _selectedRule, value))
            {
                // Update condition and action editors when rule changes
                if (value != null)
                {
                    ConditionEditor.RootCondition = value.Conditions ?? new Models.Domain.ConditionGroup { Operator = Models.Enums.LogicOperator.And };
                    ActionEditor.Actions.Clear();
                    foreach (var action in value.Actions.OrderBy(a => a.Order))
                    {
                        ActionEditor.Actions.Add(action);
                    }
                }
            }
        }
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private ConditionEditorViewModel _conditionEditor = new();

    [ObservableProperty]
    private ActionEditorViewModel _actionEditor = new();

    [ObservableProperty]
    private NavigationView _currentView = NavigationView.RuleEditor;

    public SettingsViewModel SettingsViewModel { get; }
    public LogsViewModel LogsViewModel { get; }

    public MainWindowViewModel(IRuleRepository ruleRepository, IImportExportService importExportService, IServiceProvider serviceProvider)
    {
        _ruleRepository = ruleRepository;
        _importExportService = importExportService;
        _serviceProvider = serviceProvider;
        
        // Get ViewModels from DI
        SettingsViewModel = (SettingsViewModel)serviceProvider.GetService(typeof(SettingsViewModel))!;
        LogsViewModel = (LogsViewModel)serviceProvider.GetService(typeof(LogsViewModel))!;
    }

    /// <summary>
    /// Loads all rules from the database.
    /// </summary>
    [RelayCommand]
    private async Task LoadRulesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading rules...";

        try
        {
            var rules = await _ruleRepository.GetAllAsync();
            Rules.Clear();
            foreach (var rule in rules.OrderBy(r => r.ExecutionOrder))
            {
                Rules.Add(rule);
            }
            StatusMessage = $"Loaded {rules.Count} rule(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading rules: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Creates a new rule.
    /// </summary>
    [RelayCommand]
    private void CreateNewRule()
    {
        var newRule = new Rule
        {
            Name = "New Rule",
            ExecutionOrder = Rules.Count
        };
        Rules.Add(newRule);
        SelectedRule = newRule;
        StatusMessage = "New rule created";
    }

    /// <summary>
    /// Saves the selected rule to the database.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveRule))]
    private async Task SaveRuleAsync()
    {
        if (SelectedRule == null) return;

        IsLoading = true;
        StatusMessage = "Saving rule...";

        try
        {
            // Update rule with current editor state
            SelectedRule.Conditions = ConditionEditor.RootCondition;
            SelectedRule.Actions.Clear();
            foreach (var action in ActionEditor.Actions)
            {
                SelectedRule.Actions.Add(action);
            }

            var existing = await _ruleRepository.GetByIdAsync(SelectedRule.Id);
            if (existing == null)
            {
                await _ruleRepository.AddAsync(SelectedRule);
                StatusMessage = "Rule created successfully";
            }
            else
            {
                await _ruleRepository.UpdateAsync(SelectedRule);
                StatusMessage = "Rule updated successfully";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving rule: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSaveRule() => SelectedRule != null && !string.IsNullOrWhiteSpace(SelectedRule.Name);

    /// <summary>
    /// Deletes the selected rule.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteRule))]
    private async Task DeleteRuleAsync()
    {
        if (SelectedRule == null) return;

        // Show confirmation dialog
        var result = MessageBox.Show(
            $"Are you sure you want to delete the rule '{SelectedRule.Name}'?\n\n" +
            $"This action cannot be undone.",
            "Confirm Delete Rule",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            StatusMessage = "Delete cancelled";
            return;
        }

        IsLoading = true;
        StatusMessage = "Deleting rule...";

        try
        {
            await _ruleRepository.DeleteAsync(SelectedRule.Id);
            Rules.Remove(SelectedRule);
            SelectedRule = null;
            StatusMessage = "Rule deleted successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting rule: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanDeleteRule() => SelectedRule != null;

    /// <summary>
    /// Toggles the enabled state of the selected rule.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleRule))]
    private async Task ToggleRuleAsync()
    {
        if (SelectedRule == null) return;

        SelectedRule.IsEnabled = !SelectedRule.IsEnabled;
        await SaveRuleAsync();
        StatusMessage = SelectedRule.IsEnabled ? "Rule enabled" : "Rule disabled";
    }

    private bool CanToggleRule() => SelectedRule != null;

    /// <summary>
    /// Tests the selected rule against a folder.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTestRule))]
    private async Task TestRuleAsync()
    {
        if (SelectedRule == null) return;

        // Save current rule state to editors before preview
        SelectedRule.Conditions = ConditionEditor.RootCondition;
        SelectedRule.Actions.Clear();
        foreach (var action in ActionEditor.Actions)
        {
            SelectedRule.Actions.Add(action);
        }

        var previewWindow = (Views.RulePreviewWindow)_serviceProvider.GetService(typeof(Views.RulePreviewWindow))!;
        await previewWindow.ShowPreviewAsync(SelectedRule);
    }

    private bool CanTestRule() => SelectedRule != null && SelectedRule.Conditions != null && SelectedRule.MonitoredFolders.Any();

    /// <summary>
    /// Exports the selected rule to a JSON file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportRule))]
    private async Task ExportRuleAsync()
    {
        if (SelectedRule == null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"{SelectedRule.Name}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            IsLoading = true;
            StatusMessage = "Exporting rule...";

            try
            {
                await _importExportService.ExportToFileAsync(new[] { SelectedRule }, dialog.FileName);
                StatusMessage = $"Rule exported to {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting rule: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private bool CanExportRule() => SelectedRule != null;

    /// <summary>
    /// Exports all rules to a JSON file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportAllRules))]
    private async Task ExportAllRulesAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = "all-rules.json"
        };

        if (dialog.ShowDialog() == true)
        {
            IsLoading = true;
            StatusMessage = "Exporting all rules...";

            try
            {
                await _importExportService.ExportToFileAsync(Rules, dialog.FileName);
                StatusMessage = $"Exported {Rules.Count} rule(s) to {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting rules: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private bool CanExportAllRules() => Rules.Any();

    /// <summary>
    /// Imports rules from a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ImportRulesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json"
        };

        if (dialog.ShowDialog() == true)
        {
            IsLoading = true;
            StatusMessage = "Importing rules...";

            try
            {
                // Validate first
                var fileContent = await File.ReadAllTextAsync(dialog.FileName);
                var validation = await _importExportService.ValidateRuleJsonAsync(fileContent);
                
                if (!validation.IsValid)
                {
                    StatusMessage = $"Import failed: {validation.ErrorMessage}";
                    return;
                }

                // Import and get rules
                var importedRules = await _importExportService.ImportFromFileAsync(dialog.FileName);
                var ruleList = importedRules.ToList();

                // Show confirmation with summary
                var confirmResult = MessageBox.Show(
                    $"Import {ruleList.Count} rule(s) from {Path.GetFileName(dialog.FileName)}?\n\n" +
                    $"Rules to import:\n" +
                    string.Join("\n", ruleList.Take(5).Select(r => $"  • {r.Name}")) +
                    (ruleList.Count > 5 ? $"\n  ... and {ruleList.Count - 5} more" : ""),
                    "Confirm Import",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.Yes)
                {
                    StatusMessage = "Import cancelled";
                    return;
                }

                // Import and save
                int count = 0;
                
                foreach (var rule in ruleList)
                {
                    // Assign next execution order
                    rule.ExecutionOrder = Rules.Any() ? Rules.Max(r => r.ExecutionOrder) + 1 : 0;
                    
                    await _ruleRepository.AddAsync(rule);
                    Rules.Add(rule);
                    count++;
                }

                StatusMessage = $"Imported {count} rule(s) from {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing rules: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Navigates to the log viewer.
    /// </summary>
    [RelayCommand]
    private void ViewLogs()
    {
        CurrentView = NavigationView.Logs;
        StatusMessage = "Viewing logs";
    }

    /// <summary>
    /// Navigates to settings.
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        CurrentView = NavigationView.Settings;
        StatusMessage = "Settings";
    }

    /// <summary>
    /// Navigates back to rule editor.
    /// </summary>
    [RelayCommand]
    private void NavigateToRuleEditor()
    {
        CurrentView = NavigationView.RuleEditor;
        StatusMessage = SelectedRule != null ? $"Editing rule: {SelectedRule.Name}" : "Ready";
    }
}
