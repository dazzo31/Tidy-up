using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
using TidyUp.Services.State;
using TidyUp.ViewModels.RuleEditor;

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

    [ObservableProperty]
    private ObservableCollection<Rule> _filteredRules = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterRules();
            }
        }
    }

    private Rule? _selectedRule;
    
    public Rule? SelectedRule
    {
        get => _selectedRule;
        set
        {
            if (IsRuleDirty && _selectedRule != null && value != _selectedRule)
            {
                if (!ConfirmDiscardRuleEdits())
                {
                    OnPropertyChanged(nameof(SelectedRule));
                    return;
                }
            }

            var oldRule = _selectedRule;
            
            if (SetProperty(ref _selectedRule, value))
            {
                IsRuleDirty = false;
                // Unsubscribe from old rule
                if (oldRule != null)
                {
                    oldRule.PropertyChanged -= SelectedRule_PropertyChanged;
                    oldRule.MonitoredFolders.CollectionChanged -= MonitoredFolders_CollectionChanged;
                }
                
                // Subscribe to new rule
                if (value != null)
                {
                    value.PropertyChanged += SelectedRule_PropertyChanged;
                    value.MonitoredFolders.CollectionChanged += MonitoredFolders_CollectionChanged;
                    
                    // Update condition and action editors when rule changes
                    ConditionEditor.RootCondition = value.Conditions ?? new Models.Domain.ConditionGroup { Operator = Models.Enums.LogicOperator.And };
                    ActionEditor.Actions.Clear();
                    foreach (var action in value.Actions.OrderBy(a => a.Order))
                    {
                        ActionEditor.Actions.Add(action);
                    }
                    
                    // Update preview with monitored folders
                    ConditionEditor.SetMonitoredFolders(value.MonitoredFolders.ToList());
                }
                
                // Notify all commands that depend on SelectedRule
                SaveRuleCommand.NotifyCanExecuteChanged();
                DeleteRuleCommand.NotifyCanExecuteChanged();
                ToggleRuleCommand.NotifyCanExecuteChanged();
                TestRuleCommand.NotifyCanExecuteChanged();
                ExportRuleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _windowTitle = "TidyUp - File Organization Manager";

    [ObservableProperty]
    private ConditionEditorViewModel _conditionEditor = new();

    [ObservableProperty]
    private ActionEditorViewModel _actionEditor = new();

    [ObservableProperty]
    private NavigationView _currentView = NavigationView.Dashboard;

    [ObservableProperty]
    private bool _isRuleDirty;

    public Func<string, string, bool>? ConfirmDiscardRuleCallback { get; set; }

    /// <summary>
    /// Prompts user to confirm discarding unsaved rule modifications before navigation or rule switching.
    /// </summary>
    public bool ConfirmDiscardRuleEdits()
    {
        if (!IsRuleDirty || SelectedRule == null)
            return true;

        bool proceed = ConfirmDiscardRuleCallback != null
            ? ConfirmDiscardRuleCallback($"You have unsaved changes to rule '{SelectedRule.Name}'. Discard unsaved changes?", "Discard Unsaved Changes")
            : MessageBox.Show(
                $"You have unsaved changes to rule '{SelectedRule.Name}'. Discard unsaved changes?",
                "Discard Unsaved Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;

        if (proceed)
        {
            IsRuleDirty = false;
        }

        return proceed;
    }

    public DashboardViewModel DashboardViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public LogsViewModel LogsViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }
    public IApplicationStateManager StateManager { get; }

    public MainWindowViewModel(
        IRuleRepository ruleRepository,
        IImportExportService importExportService,
        IServiceProvider serviceProvider,
        IApplicationStateManager? stateManager = null)
    {
        _ruleRepository = ruleRepository;
        _importExportService = importExportService;
        _serviceProvider = serviceProvider;
        
        StateManager = stateManager
            ?? (IApplicationStateManager?)serviceProvider.GetService(typeof(IApplicationStateManager))
            ?? new ApplicationStateManager();

        StateManager.StateChanged += OnStateChanged;
        UpdateTitleAndStateProperties();

        // Get ViewModels from DI
        DashboardViewModel = (DashboardViewModel)serviceProvider.GetService(typeof(DashboardViewModel))!;
        SettingsViewModel = (SettingsViewModel)serviceProvider.GetService(typeof(SettingsViewModel))!;
        LogsViewModel = (LogsViewModel)serviceProvider.GetService(typeof(LogsViewModel))!;
        HistoryViewModel = (HistoryViewModel)serviceProvider.GetService(typeof(HistoryViewModel))!;

        DashboardViewModel.RequestNavigate += view =>
        {
            if (!ConfirmDiscardRuleEdits()) return;
            CurrentView = view;
            StatusMessage = view.ToString();
        };
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        UpdateTitleAndStateProperties();
        CreateNewRuleCommand.NotifyCanExecuteChanged();
        SaveRuleCommand.NotifyCanExecuteChanged();
        DeleteRuleCommand.NotifyCanExecuteChanged();
        ToggleRuleCommand.NotifyCanExecuteChanged();
        TestRuleCommand.NotifyCanExecuteChanged();
    }

    private void UpdateTitleAndStateProperties()
    {
        WindowTitle = StateManager.CurrentState switch
        {
            ApplicationLifecycleState.ConfiguringRule => "TidyUp - File Organization Manager [Configuring Rule (Unvalidated)]",
            ApplicationLifecycleState.Simulating => "TidyUp - File Organization Manager [Simulating Rule...]",
            ApplicationLifecycleState.PreviewReady => "TidyUp - File Organization Manager [Preview Ready]",
            ApplicationLifecycleState.ExecutingBatch => "TidyUp - File Organization Manager [Executing Operations...]",
            ApplicationLifecycleState.MonitoringRunning => "TidyUp - File Organization Manager [Monitoring Active]",
            ApplicationLifecycleState.MonitoringPaused => "TidyUp - File Organization Manager [Monitoring Paused]",
            _ => "TidyUp - File Organization Manager"
        };

        OnPropertyChanged(nameof(StateBadgeText));
        OnPropertyChanged(nameof(StateBadgeBackground));
        OnPropertyChanged(nameof(StateBadgeForeground));
        OnPropertyChanged(nameof(StateBadgeIcon));
    }

    public string StateBadgeText => StateManager.CurrentState switch
    {
        ApplicationLifecycleState.Idle => "IDLE",
        ApplicationLifecycleState.ConfiguringRule => "CONFIGURING",
        ApplicationLifecycleState.Simulating => "SIMULATING",
        ApplicationLifecycleState.PreviewReady => "PREVIEW READY",
        ApplicationLifecycleState.ExecutingBatch => "EXECUTING",
        ApplicationLifecycleState.MonitoringRunning => "MONITORING ACTIVE",
        ApplicationLifecycleState.MonitoringPaused => "MONITORING PAUSED",
        _ => StateManager.CurrentState.ToString().ToUpperInvariant()
    };

    public string StateBadgeBackground => StateManager.CurrentState switch
    {
        ApplicationLifecycleState.Idle => "#ECEFF1",
        ApplicationLifecycleState.ConfiguringRule => "#FFF3E0",
        ApplicationLifecycleState.Simulating => "#E3F2FD",
        ApplicationLifecycleState.PreviewReady => "#E8F5E9",
        ApplicationLifecycleState.ExecutingBatch => "#FFEBEE",
        ApplicationLifecycleState.MonitoringRunning => "#E8F5E9",
        ApplicationLifecycleState.MonitoringPaused => "#FFF8E1",
        _ => "#EEEEEE"
    };

    public string StateBadgeForeground => StateManager.CurrentState switch
    {
        ApplicationLifecycleState.Idle => "#455A64",
        ApplicationLifecycleState.ConfiguringRule => "#E65100",
        ApplicationLifecycleState.Simulating => "#1565C0",
        ApplicationLifecycleState.PreviewReady => "#2E7D32",
        ApplicationLifecycleState.ExecutingBatch => "#C62828",
        ApplicationLifecycleState.MonitoringRunning => "#1B5E20",
        ApplicationLifecycleState.MonitoringPaused => "#F57F17",
        _ => "#424242"
    };

    public string StateBadgeIcon => StateManager.CurrentState switch
    {
        ApplicationLifecycleState.Idle => "CircleOutline",
        ApplicationLifecycleState.ConfiguringRule => "Pencil",
        ApplicationLifecycleState.Simulating => "TestTube",
        ApplicationLifecycleState.PreviewReady => "CheckCircleOutline",
        ApplicationLifecycleState.ExecutingBatch => "CogSync",
        ApplicationLifecycleState.MonitoringRunning => "RadioboxMarked",
        ApplicationLifecycleState.MonitoringPaused => "PauseCircle",
        _ => "Information"
    };

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
            FilterRules();
            await DashboardViewModel.LoadDashboardDataAsync();
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
    /// Launches the guided 4-stage rule creation wizard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreateNewRule))]
    private async Task CreateNewRuleAsync()
    {
        var wizardVm = (RuleWizardViewModel)_serviceProvider.GetService(typeof(RuleWizardViewModel))!;
        var wizardWindow = new Views.RuleEditor.RuleWizardView(wizardVm);

        if (Application.Current?.MainWindow != null)
        {
            wizardWindow.Owner = Application.Current.MainWindow;
        }

        var result = wizardWindow.ShowDialog();
        if (result == true && wizardVm.CreatedRule != null)
        {
            await LoadRulesAsync();
            SelectedRule = Rules.FirstOrDefault(r => r.Id == wizardVm.CreatedRule.Id);
            CurrentView = NavigationView.RuleEditor;
            StatusMessage = $"Rule '{wizardVm.CreatedRule.Name}' created successfully (Disabled)";
        }
    }

    private bool CanCreateNewRule() => StateManager.CurrentState != ApplicationLifecycleState.ExecutingBatch;

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
            IsRuleDirty = false;
            StateManager.NotifyRuleSaved();
        }
    }

    private bool CanSaveRule() =>
        SelectedRule != null &&
        !string.IsNullOrWhiteSpace(SelectedRule.Name) &&
        StateManager.CurrentState != ApplicationLifecycleState.ExecutingBatch;

    /// <summary>
    /// Filters rules based on search text.
    /// </summary>
    private void FilterRules()
    {
        FilteredRules.Clear();
        
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // No filter - show all rules
            foreach (var rule in Rules)
            {
                FilteredRules.Add(rule);
            }
        }
        else
        {
            // Filter by name or description
            var searchLower = SearchText.ToLowerInvariant();
            foreach (var rule in Rules)
            {
                if (rule.Name.ToLowerInvariant().Contains(searchLower) ||
                    (rule.Description?.ToLowerInvariant().Contains(searchLower) ?? false))
                {
                    FilteredRules.Add(rule);
                }
            }
        }
    }

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
            FilterRules();
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

    private bool CanDeleteRule() =>
        SelectedRule != null &&
        StateManager.CurrentState != ApplicationLifecycleState.ExecutingBatch;

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

    private bool CanToggleRule() =>
        SelectedRule != null &&
        StateManager.CurrentState != ApplicationLifecycleState.ExecutingBatch;

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

        StateManager.TransitionTo(ApplicationLifecycleState.Simulating, "Testing rule preview");

        try
        {
            var previewWindow = _serviceProvider.GetService(typeof(Views.PreviewWindow)) as Views.PreviewWindow;
            if (previewWindow != null)
            {
                await previewWindow.ShowPreviewAsync(SelectedRule);
            }
            else
            {
                var legacyPreviewWindow = (Views.RulePreviewWindow)_serviceProvider.GetService(typeof(Views.RulePreviewWindow))!;
                await legacyPreviewWindow.ShowPreviewAsync(SelectedRule);
            }

            StateManager.NotifyRuleSimulated();
        }
        catch
        {
            StateManager.TryTransitionTo(ApplicationLifecycleState.Idle);
            throw;
        }
    }

    private bool CanTestRule() =>
        SelectedRule != null &&
        SelectedRule.Conditions != null &&
        SelectedRule.MonitoredFolders.Any() &&
        StateManager.CurrentState != ApplicationLifecycleState.ExecutingBatch &&
        StateManager.CurrentState != ApplicationLifecycleState.Simulating;

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
        if (!ConfirmDiscardRuleEdits()) return;
        CurrentView = NavigationView.Logs;
        StatusMessage = "Viewing logs";
    }

    /// <summary>
    /// Navigates to the operation history and rollback journal.
    /// </summary>
    [RelayCommand]
    private void ViewHistory()
    {
        if (!ConfirmDiscardRuleEdits()) return;
        CurrentView = NavigationView.History;
        StatusMessage = "History & Rollback Journal";
    }

    /// <summary>
    /// Navigates to settings.
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        if (!ConfirmDiscardRuleEdits()) return;
        CurrentView = NavigationView.Settings;
        StatusMessage = "Settings";
    }

    /// <summary>
    /// Opens the Help window.
    /// </summary>
    [RelayCommand]
    private void ViewHelp()
    {
        var helpWindow = new Views.HelpWindow();
        helpWindow.ShowDialog();
        StatusMessage = "Help";
    }

    /// <summary>
    /// Navigates to operational dashboard overview.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToDashboardAsync()
    {
        if (!ConfirmDiscardRuleEdits()) return;
        CurrentView = NavigationView.Dashboard;
        StatusMessage = "Dashboard";
        await DashboardViewModel.LoadDashboardDataAsync();
    }

    /// <summary>
    /// Navigates back to rule editor.
    /// </summary>
    [RelayCommand]
    private void NavigateToRuleEditor()
    {
        CurrentView = NavigationView.RuleEditor;
        StatusMessage = "Rule Editor";
    }

    /// <summary>
    /// Event handler for SelectedRule property changes.
    /// </summary>
    private void SelectedRule_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsRuleDirty = true;
        StateManager.NotifyRuleModified(SelectedRule?.Name);
        if (e.PropertyName == nameof(Rule.Name))
        {
            SaveRuleCommand.NotifyCanExecuteChanged();
        }
        else if (e.PropertyName == nameof(Rule.Conditions))
        {
            TestRuleCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Event handler for MonitoredFolders collection changes.
    /// </summary>
    private void MonitoredFolders_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsRuleDirty = true;
        StateManager.NotifyRuleModified(SelectedRule?.Name);
        TestRuleCommand.NotifyCanExecuteChanged();
    }
}
