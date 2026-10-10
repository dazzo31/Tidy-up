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
using TidyUp.Services.Monitoring;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Services.State;
using TidyUp.Services.Validation;
using TidyUp.ViewModels.RuleEditor;

namespace TidyUp.ViewModels;

public record RuleSortOption(RuleSortType Type, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// ViewModel for the main window.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IImportExportService _importExportService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IRuleSummaryGenerator _ruleSummaryGenerator;
    private readonly IRuleValidator _ruleValidator;

    public DashboardViewModel DashboardViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public LogsViewModel LogsViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }
    public RuleWizardViewModel WizardViewModel { get; }

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

    public static IReadOnlyList<RuleSortOption> AvailableSortOptions { get; } = new[]
    {
        new RuleSortOption(RuleSortType.ExecutionPriority, "Priority"),
        new RuleSortOption(RuleSortType.Name, "Name"),
        new RuleSortOption(RuleSortType.LastRun, "Last Run"),
        new RuleSortOption(RuleSortType.ModificationDate, "Modified")
    };

    [ObservableProperty]
    private RuleSortOption _selectedSortOption = AvailableSortOptions[0];

    [ObservableProperty]
    private RuleFilterType _selectedFilter = RuleFilterType.All;

    public bool IsFilterAllActive => SelectedFilter == RuleFilterType.All;
    public bool IsFilterEnabledActive => SelectedFilter == RuleFilterType.EnabledOnly;
    public bool IsFilterDisabledActive => SelectedFilter == RuleFilterType.DisabledOnly;
    public bool IsFilterErrorsActive => SelectedFilter == RuleFilterType.HasErrors;

    public double FilterAllOpacity => IsFilterAllActive ? 1.0 : 0.6;
    public double FilterEnabledOpacity => IsFilterEnabledActive ? 1.0 : 0.6;
    public double FilterDisabledOpacity => IsFilterDisabledActive ? 1.0 : 0.6;
    public double FilterErrorsOpacity => IsFilterErrorsActive ? 1.0 : 0.6;

    public string FilterAllFontWeight => IsFilterAllActive ? "Bold" : "Normal";
    public string FilterEnabledFontWeight => IsFilterEnabledActive ? "Bold" : "Normal";
    public string FilterDisabledFontWeight => IsFilterDisabledActive ? "Bold" : "Normal";
    public string FilterErrorsFontWeight => IsFilterErrorsActive ? "Bold" : "Normal";

    [RelayCommand]
    public void SetFilter(RuleFilterType filter)
    {
        if (SelectedFilter == filter)
        {
            FilterRules();
        }
        else
        {
            SelectedFilter = filter;
        }
    }

    partial void OnSelectedFilterChanged(RuleFilterType value)
    {
        OnPropertyChanged(nameof(IsFilterAllActive));
        OnPropertyChanged(nameof(IsFilterEnabledActive));
        OnPropertyChanged(nameof(IsFilterDisabledActive));
        OnPropertyChanged(nameof(IsFilterErrorsActive));
        OnPropertyChanged(nameof(FilterAllOpacity));
        OnPropertyChanged(nameof(FilterEnabledOpacity));
        OnPropertyChanged(nameof(FilterDisabledOpacity));
        OnPropertyChanged(nameof(FilterErrorsOpacity));
        OnPropertyChanged(nameof(FilterAllFontWeight));
        OnPropertyChanged(nameof(FilterEnabledFontWeight));
        OnPropertyChanged(nameof(FilterDisabledFontWeight));
        OnPropertyChanged(nameof(FilterErrorsFontWeight));
        FilterRules();
    }

    partial void OnSelectedSortOptionChanged(RuleSortOption value)
    {
        FilterRules();
    }

    [ObservableProperty]
    private RuleEditorMode _currentEditorMode = RuleEditorMode.GuidedWorkflow;

    public bool IsGuidedModeActive => CurrentEditorMode == RuleEditorMode.GuidedWorkflow;
    public bool IsAdvancedModeActive => CurrentEditorMode == RuleEditorMode.AdvancedTabs;
    public double GuidedModeOpacity => IsGuidedModeActive ? 1.0 : 0.6;
    public double AdvancedModeOpacity => IsAdvancedModeActive ? 1.0 : 0.6;
    public string GuidedModeFontWeight => IsGuidedModeActive ? "Bold" : "Normal";
    public string AdvancedModeFontWeight => IsAdvancedModeActive ? "Bold" : "Normal";

    [RelayCommand]
    public void SetEditorMode(RuleEditorMode mode)
    {
        if (CurrentEditorMode == mode) return;

        if (mode == RuleEditorMode.AdvancedTabs)
        {
            if (SelectedRule != null)
            {
                WizardViewModel.SyncToRule(SelectedRule);
                ConditionEditor.RootCondition = SelectedRule.Conditions ?? new Models.Domain.ConditionGroup { Operator = Models.Enums.LogicOperator.And };
                ActionEditor.Actions.Clear();
                foreach (var action in SelectedRule.Actions.OrderBy(a => a.Order))
                {
                    ActionEditor.Actions.Add(action);
                }
                ConditionEditor.SetMonitoredFolders(SelectedRule.MonitoredFolders.ToList());
            }
        }
        else if (mode == RuleEditorMode.GuidedWorkflow)
        {
            if (SelectedRule != null)
            {
                SelectedRule.Conditions = ConditionEditor.RootCondition;
                SelectedRule.Actions.Clear();
                foreach (var action in ActionEditor.Actions)
                {
                    SelectedRule.Actions.Add(action);
                }
                WizardViewModel.LoadFromRule(SelectedRule);
            }
        }

        CurrentEditorMode = mode;
        OnPropertyChanged(nameof(IsGuidedModeActive));
        OnPropertyChanged(nameof(IsAdvancedModeActive));
        OnPropertyChanged(nameof(GuidedModeOpacity));
        OnPropertyChanged(nameof(AdvancedModeOpacity));
        OnPropertyChanged(nameof(GuidedModeFontWeight));
        OnPropertyChanged(nameof(AdvancedModeFontWeight));
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

                    // Sync to wizard viewmodel
                    WizardViewModel?.LoadFromRule(value);
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

    public IApplicationStateManager StateManager { get; }
    private readonly IWatcherHealthMonitor? _healthMonitor;
    private string _activeAlertSummary = string.Empty;

    public MainWindowViewModel(
        IRuleRepository ruleRepository,
        IImportExportService importExportService,
        IServiceProvider serviceProvider,
        IApplicationStateManager? stateManager = null,
        IWatcherHealthMonitor? healthMonitor = null,
        IRuleSummaryGenerator? ruleSummaryGenerator = null,
        IRuleValidator? ruleValidator = null)
    {
        _ruleRepository = ruleRepository;
        _importExportService = importExportService;
        _serviceProvider = serviceProvider;

        _ruleSummaryGenerator = ruleSummaryGenerator
            ?? (IRuleSummaryGenerator?)serviceProvider.GetService(typeof(IRuleSummaryGenerator))
            ?? new RuleSummaryGenerator();

        _ruleValidator = ruleValidator
            ?? (IRuleValidator?)serviceProvider.GetService(typeof(IRuleValidator))
            ?? new RuleValidator();

        _rules.CollectionChanged += (s, e) => FilterRules();
        
        StateManager = stateManager
            ?? (IApplicationStateManager?)serviceProvider.GetService(typeof(IApplicationStateManager))
            ?? new ApplicationStateManager();

        _healthMonitor = healthMonitor
            ?? (IWatcherHealthMonitor?)serviceProvider.GetService(typeof(IWatcherHealthMonitor));

        StateManager.StateChanged += OnStateChanged;
        
        if (_healthMonitor != null)
        {
            _healthMonitor.FolderHealthChanged += (s, e) => UpdateHealthAlerts();
            UpdateHealthAlerts();
        }

        UpdateTitleAndStateProperties();

        // Get ViewModels from DI
        DashboardViewModel = (DashboardViewModel)serviceProvider.GetService(typeof(DashboardViewModel))!;
        SettingsViewModel = (SettingsViewModel)serviceProvider.GetService(typeof(SettingsViewModel))!;
        LogsViewModel = (LogsViewModel)serviceProvider.GetService(typeof(LogsViewModel))!;
        HistoryViewModel = (HistoryViewModel)serviceProvider.GetService(typeof(HistoryViewModel))!;

        var executionPlanGen = (IExecutionPlanGenerator?)serviceProvider.GetService(typeof(IExecutionPlanGenerator))
            ?? new ExecutionPlanGenerator(
                (IRuleEngine?)serviceProvider.GetService(typeof(IRuleEngine)) ?? new RuleEngine(),
                (IVariableEngine?)serviceProvider.GetService(typeof(IVariableEngine)) ?? new VariableEngine());

        WizardViewModel = (RuleWizardViewModel?)serviceProvider.GetService(typeof(RuleWizardViewModel))
            ?? new RuleWizardViewModel(_ruleRepository, _ruleValidator, executionPlanGen, _ruleSummaryGenerator);

        WizardViewModel.RuleSaved += async (s, savedRule) =>
        {
            await LoadRulesAsync();
            SelectedRule = Rules.FirstOrDefault(r => r.Id == savedRule.Id) ?? savedRule;
            IsRuleDirty = false;
            StatusMessage = $"Rule '{savedRule.Name}' saved successfully";
        };

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
            ApplicationLifecycleState.MonitoringRunning => AlertCount > 0 
                ? $"TidyUp - File Organization Manager [Monitoring (Degraded - {AlertCount} Alert{(AlertCount == 1 ? "" : "s")})]"
                : "TidyUp - File Organization Manager [Monitoring Active]",
            ApplicationLifecycleState.MonitoringPaused => "TidyUp - File Organization Manager [Monitoring Paused]",
            _ => "TidyUp - File Organization Manager"
        };

        OnPropertyChanged(nameof(StateBadgeText));
        OnPropertyChanged(nameof(StateBadgeBackground));
        OnPropertyChanged(nameof(StateBadgeForeground));
        OnPropertyChanged(nameof(StateBadgeIcon));
        OnPropertyChanged(nameof(StateBadgeTooltip));
    }

    private int _alertCount;
    public int AlertCount
    {
        get => _alertCount;
        set
        {
            if (SetProperty(ref _alertCount, value))
            {
                OnPropertyChanged(nameof(HasActiveAlerts));
                OnPropertyChanged(nameof(AlertBadgeText));
                OnPropertyChanged(nameof(AlertBadgeTooltip));
                OnPropertyChanged(nameof(StateBadgeText));
                OnPropertyChanged(nameof(StateBadgeBackground));
                OnPropertyChanged(nameof(StateBadgeForeground));
                OnPropertyChanged(nameof(StateBadgeIcon));
                OnPropertyChanged(nameof(StateBadgeTooltip));
            }
        }
    }

    public bool HasActiveAlerts => AlertCount > 0;
    public string AlertBadgeText => $"{AlertCount} Alert{(AlertCount == 1 ? "" : "s")}";
    public string AlertBadgeTooltip => string.IsNullOrWhiteSpace(_activeAlertSummary)
        ? "One or more monitored folders require attention (disconnected, degraded, or permissions error)."
        : _activeAlertSummary;

    public void UpdateHealthAlerts()
    {
        if (_healthMonitor == null) return;
        var allHealth = _healthMonitor.GetAllFolderHealth();
        var warningFolders = allHealth.Values
            .Where(h => h.Status == Services.Monitoring.WatcherHealthStatus.Degraded 
                     || h.Status == Services.Monitoring.WatcherHealthStatus.Disconnected 
                     || h.Status == Services.Monitoring.WatcherHealthStatus.Error)
            .ToList();

        AlertCount = warningFolders.Count;
        _activeAlertSummary = warningFolders.Count > 0
            ? string.Join("; ", warningFolders.Select(w => $"{(Path.GetFileName(w.Path) is { Length: > 0 } n ? n : w.Path)}: {w.Status} ({w.StatusDetail ?? "Check folder access"})"))
            : string.Empty;

        UpdateTitleAndStateProperties();
    }

    public string StateBadgeText => StateManager.CurrentState switch
    {
        ApplicationLifecycleState.Idle => "IDLE",
        ApplicationLifecycleState.ConfiguringRule => "CONFIGURING",
        ApplicationLifecycleState.Simulating => "SIMULATING",
        ApplicationLifecycleState.PreviewReady => "PREVIEW READY",
        ApplicationLifecycleState.ExecutingBatch => "EXECUTING",
        ApplicationLifecycleState.MonitoringRunning => AlertCount > 0 ? "MONITORING (DEGRADED)" : "MONITORING ACTIVE",
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
        ApplicationLifecycleState.MonitoringRunning => AlertCount > 0 ? "#FFF3E0" : "#E8F5E9",
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
        ApplicationLifecycleState.MonitoringRunning => AlertCount > 0 ? "#E65100" : "#1B5E20",
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
        ApplicationLifecycleState.MonitoringRunning => AlertCount > 0 ? "AlertCircle" : "RadioboxMarked",
        ApplicationLifecycleState.MonitoringPaused => "PauseCircle",
        _ => "Information"
    };

    public string StateBadgeTooltip => StateManager.CurrentState switch
    {
        ApplicationLifecycleState.MonitoringRunning => AlertCount > 0
            ? $"Monitoring active with {AlertCount} alert(s): {_activeAlertSummary}"
            : "File system monitoring is active and healthy across all watched folders.",
        ApplicationLifecycleState.MonitoringPaused => "File monitoring is temporarily paused.",
        ApplicationLifecycleState.ExecutingBatch => "Executing automated file operations batch...",
        ApplicationLifecycleState.Simulating => "Running safe read-only rule simulation...",
        ApplicationLifecycleState.PreviewReady => "Rule preview ready. Review planned actions before execution.",
        ApplicationLifecycleState.ConfiguringRule => "Configuring rule conditions, folders, and actions.",
        _ => "Application is idle. Select a rule or view to begin."
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
                UpdateRuleMetadata(rule);
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
    /// Creates a new rule defaulting to Disabled in Guided Workflow mode.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreateNewRule))]
    private void CreateNewRule()
    {
        if (!ConfirmDiscardRuleEdits()) return;

        var newRule = new Rule
        {
            Name = "New Rule",
            IsEnabled = false
        };

        WizardViewModel.ResetWizard();
        WizardViewModel.RuleName = "New Rule";
        CurrentEditorMode = RuleEditorMode.GuidedWorkflow;
        CurrentView = NavigationView.RuleEditor;
        SelectedRule = newRule;
        IsRuleDirty = true;
        StatusMessage = "Creating new rule in Guided Workflow (Disabled)";
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
            if (CurrentEditorMode == RuleEditorMode.GuidedWorkflow)
            {
                WizardViewModel.SyncToRule(SelectedRule);
            }
            else
            {
                SelectedRule.Conditions = ConditionEditor.RootCondition;
                SelectedRule.Actions.Clear();
                foreach (var action in ActionEditor.Actions)
                {
                    SelectedRule.Actions.Add(action);
                }
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
            if (SelectedRule != null)
            {
                SelectedRule.IsDirty = false;
                UpdateRuleMetadata(SelectedRule);
            }
            FilterRules();
            StateManager.NotifyRuleSaved();
        }
    }

    private bool CanSaveRule() =>
        SelectedRule != null &&
        !string.IsNullOrWhiteSpace(SelectedRule.Name) &&
        StateManager.CurrentState != ApplicationLifecycleState.ExecutingBatch;

    /// <summary>
    /// Generates plain-language natural language summary and performs validation on the rule.
    /// </summary>
    public void UpdateRuleMetadata(Rule rule)
    {
        if (rule == null) return;
        rule.Summary = _ruleSummaryGenerator.GenerateSummary(rule);
        var validation = _ruleValidator.ValidateRule(rule);
        rule.HasErrors = !validation.IsValid;
        rule.ErrorMessage = validation.IsValid ? null : string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
    }

    /// <summary>
    /// Filters and sorts rules based on search text, active filter chips, and selected sort order.
    /// </summary>
    public void FilterRules()
    {
        var query = Rules.AsEnumerable();
        
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            query = query.Where(rule =>
                (rule.Name != null && rule.Name.ToLowerInvariant().Contains(searchLower)) ||
                (rule.Description != null && rule.Description.ToLowerInvariant().Contains(searchLower)) ||
                (rule.Summary != null && rule.Summary.ToLowerInvariant().Contains(searchLower)));
        }

        query = SelectedFilter switch
        {
            RuleFilterType.EnabledOnly => query.Where(r => r.IsEnabled),
            RuleFilterType.DisabledOnly => query.Where(r => !r.IsEnabled),
            RuleFilterType.HasErrors => query.Where(r => r.HasErrors),
            _ => query
        };

        var sortType = SelectedSortOption?.Type ?? RuleSortType.ExecutionPriority;
        query = sortType switch
        {
            RuleSortType.Name => query.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
            RuleSortType.LastRun => query.OrderByDescending(r => r.LastRunDate ?? DateTime.MinValue),
            RuleSortType.ModificationDate => query.OrderByDescending(r => r.ModifiedDate),
            _ => query.OrderBy(r => r.ExecutionOrder)
        };

        var filtered = query.ToList();
        FilteredRules.Clear();
        foreach (var rule in filtered)
        {
            FilteredRules.Add(rule);
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
            var previewWindow = (Views.PreviewWindow)_serviceProvider.GetService(typeof(Views.PreviewWindow))!;
            await previewWindow.ShowPreviewAsync(SelectedRule);

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
    /// Navigates to the embedded Help view.
    /// </summary>
    [RelayCommand]
    private void ViewHelp()
    {
        CurrentView = NavigationView.Help;
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
    /// Navigates to the monitored folders hub.
    /// </summary>
    [RelayCommand]
    private void NavigateToFolders()
    {
        if (!ConfirmDiscardRuleEdits()) return;
        CurrentView = NavigationView.Folders;
        StatusMessage = "Monitored Folders Hub";
    }

    /// <summary>
    /// Navigates to the preview and review workspace.
    /// </summary>
    [RelayCommand]
    private void NavigateToPreviewReview()
    {
        if (!ConfirmDiscardRuleEdits()) return;
        CurrentView = NavigationView.PreviewReview;
        StatusMessage = "Preview & Review Workspace";
    }

    partial void OnCurrentViewChanged(NavigationView value)
    {
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsRulesActive));
        OnPropertyChanged(nameof(IsFoldersActive));
        OnPropertyChanged(nameof(IsPreviewActive));
        OnPropertyChanged(nameof(IsHistoryActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    public bool IsDashboardActive => CurrentView == NavigationView.Dashboard;
    public bool IsRulesActive => CurrentView == NavigationView.RuleEditor;
    public bool IsFoldersActive => CurrentView == NavigationView.Folders;
    public bool IsPreviewActive => CurrentView == NavigationView.PreviewReview;
    public bool IsHistoryActive => CurrentView == NavigationView.History;
    public bool IsSettingsActive => CurrentView == NavigationView.Settings;

    /// <summary>
    /// Event handler for SelectedRule property changes.
    /// </summary>
    private void SelectedRule_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Rule.IsDirty) ||
            e.PropertyName == nameof(Rule.Summary) ||
            e.PropertyName == nameof(Rule.HasErrors) ||
            e.PropertyName == nameof(Rule.ErrorMessage))
        {
            return;
        }

        IsRuleDirty = true;
        if (SelectedRule != null)
        {
            SelectedRule.IsDirty = true;
            UpdateRuleMetadata(SelectedRule);
        }

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
        if (SelectedRule != null)
        {
            SelectedRule.IsDirty = true;
            UpdateRuleMetadata(SelectedRule);
        }
        StateManager.NotifyRuleModified(SelectedRule?.Name);
        TestRuleCommand.NotifyCanExecuteChanged();
    }
}
