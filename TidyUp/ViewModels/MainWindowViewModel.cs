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
using TidyUp.Services.Diagnostics;
using TidyUp.Services.Folders;
using TidyUp.Services.Monitoring;
using TidyUp.Services.Processing;
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
    private readonly IRuleSerializationService _ruleSerializationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IRuleSummaryGenerator _ruleSummaryGenerator;
    private readonly IRuleValidator _ruleValidator;
    private readonly IFolderPathNormalizer _pathNormalizer;
    private readonly IMonitoredFolderCatalog? _folderCatalog;

    public DashboardViewModel DashboardViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public LogsViewModel LogsViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }
    public RuleWizardViewModel WizardViewModel { get; }
    public FoldersViewModel FoldersViewModel { get; }
    public PreviewViewModel PreviewViewModel { get; }

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
                OnPropertyChanged(nameof(CanReorderRules));
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
        OnPropertyChanged(nameof(CanReorderRules));
    }

    partial void OnSelectedSortOptionChanged(RuleSortOption value)
    {
        FilterRules();
        OnPropertyChanged(nameof(CanReorderRules));
    }

    /// <summary>
    /// Gets whether drag-and-drop reordering is permitted.
    /// Reordering is disabled when search filtering, non-All status filter, or non-priority sorting is active.
    /// </summary>
    public bool CanReorderRules =>
        string.IsNullOrWhiteSpace(SearchText) &&
        SelectedFilter == RuleFilterType.All &&
        (SelectedSortOption == null || SelectedSortOption.Type == RuleSortType.ExecutionPriority);

    /// <summary>
    /// Reorders rules in memory and commits updated execution orders atomically to SQLite.
    /// If persistence fails, restores previous in-memory order and surfaces the error.
    /// </summary>
    public async Task<bool> ReorderRulesAsync(Rule sourceRule, Rule targetRule)
    {
        ArgumentNullException.ThrowIfNull(sourceRule);
        ArgumentNullException.ThrowIfNull(targetRule);

        if (!CanReorderRules)
        {
            StatusMessage = "Reordering is disabled while searching, filtering, or sorting by non-priority criteria.";
            return false;
        }

        if (sourceRule == targetRule)
            return false;

        int oldIndex = Rules.IndexOf(sourceRule);
        int newIndex = Rules.IndexOf(targetRule);

        if (oldIndex == -1 || newIndex == -1 || oldIndex == newIndex)
            return false;

        // Snapshot previous orderings for rollback on transaction failure
        var previousOrderings = Rules.Select(r => (Rule: r, Order: r.ExecutionOrder)).ToList();

        // Apply in-memory reordering
        Rules.Move(oldIndex, newIndex);
        for (int i = 0; i < Rules.Count; i++)
        {
            Rules[i].ExecutionOrder = i;
        }

        FilterRules();

        var payload = Rules.Select(r => (r.Id, r.ExecutionOrder)).ToList();

        try
        {
            await _ruleRepository.UpdateExecutionOrdersAsync(payload);
            StatusMessage = "Rule execution order updated successfully";
            return true;
        }
        catch (Exception ex)
        {
            // Restore previous in-memory state on persistence failure
            Rules.Move(newIndex, oldIndex);
            foreach (var item in previousOrderings)
            {
                item.Rule.ExecutionOrder = item.Order;
            }
            FilterRules();

            StatusMessage = $"Failed to save rule reordering: {ex.Message}";
            return false;
        }
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
    private RuleDraft? _activeRuleDraft;
    private bool _isSynchronizing;
    
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
            
            _isSynchronizing = true;
            try
            {
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
                        _activeRuleDraft = new RuleDraft(value);
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
                    else
                    {
                        _activeRuleDraft = null;
                    }
                    
                    // Notify all commands that depend on SelectedRule
                    SaveRuleCommand.NotifyCanExecuteChanged();
                    DeleteRuleCommand.NotifyCanExecuteChanged();
                    ToggleRuleCommand.NotifyCanExecuteChanged();
                    TestRuleCommand.NotifyCanExecuteChanged();
                    ExportRuleCommand.NotifyCanExecuteChanged();
                }
            }
            finally
            {
                _isSynchronizing = false;
                IsRuleDirty = false;
                if (value != null) value.IsDirty = false;
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
            DiscardActiveEdits();
        }

        return proceed;
    }

    public void HandleWizardCancel()
    {
        if (!ConfirmDiscardRuleEdits())
            return;

        StatusMessage = "Ready";
    }

    private void DiscardActiveEdits()
    {
        if (SelectedRule == null)
        {
            IsRuleDirty = false;
            return;
        }

        if (_activeRuleDraft?.IsNew == true)
        {
            var fallback = Rules.FirstOrDefault();
            _selectedRule = null;
            IsRuleDirty = false;
            WizardViewModel?.ResetWizard();
            SelectedRule = fallback;
            return;
        }

        if (_activeRuleDraft != null)
        {
            RestoreRuleFromBaseline(_activeRuleDraft.BaselineSnapshot);
        }

        IsRuleDirty = false;
        SelectedRule.IsDirty = false;
    }

    private void RestoreRuleFromBaseline(Rule baseline)
    {
        if (SelectedRule == null || baseline == null) return;

        _isSynchronizing = true;
        try
        {
            SelectedRule.PropertyChanged -= SelectedRule_PropertyChanged;
            SelectedRule.MonitoredFolders.CollectionChanged -= MonitoredFolders_CollectionChanged;

            SelectedRule.Name = baseline.Name;
            SelectedRule.Description = baseline.Description;
            SelectedRule.IsEnabled = baseline.IsEnabled;
            SelectedRule.ExecutionOrder = baseline.ExecutionOrder;
            SelectedRule.StopProcessingAfterMatch = baseline.StopProcessingAfterMatch;
            SelectedRule.TriggerType = baseline.TriggerType;
            SelectedRule.ScheduledTime = baseline.ScheduledTime;
            SelectedRule.CreatedDate = baseline.CreatedDate;
            SelectedRule.ModifiedDate = baseline.ModifiedDate;
            SelectedRule.LastRunDate = baseline.LastRunDate;
            SelectedRule.FilesProcessedCount = baseline.FilesProcessedCount;

            SelectedRule.MonitoredFolders.Clear();
            foreach (var folder in baseline.MonitoredFolders)
            {
                SelectedRule.MonitoredFolders.Add(new MonitoredFolder
                {
                    Path = folder.Path,
                    IncludeSubfolders = folder.IncludeSubfolders,
                    ExclusionPatterns = new System.Collections.ObjectModel.ObservableCollection<string>(folder.ExclusionPatterns)
                });
            }

            var cloned = RuleDraft.DeepCloneRule(baseline);
            SelectedRule.Conditions = cloned.Conditions;
            SelectedRule.Actions.Clear();
            foreach (var action in cloned.Actions)
            {
                SelectedRule.Actions.Add(action);
            }

            UpdateRuleMetadata(SelectedRule);

            ConditionEditor.RootCondition = SelectedRule.Conditions ?? new Models.Domain.ConditionGroup { Operator = Models.Enums.LogicOperator.And };
            ActionEditor.Actions.Clear();
            foreach (var action in SelectedRule.Actions.OrderBy(a => a.Order))
            {
                ActionEditor.Actions.Add(action);
            }
            ConditionEditor.SetMonitoredFolders(SelectedRule.MonitoredFolders.ToList());

            WizardViewModel?.LoadFromRule(SelectedRule);

            SelectedRule.PropertyChanged += SelectedRule_PropertyChanged;
            SelectedRule.MonitoredFolders.CollectionChanged += MonitoredFolders_CollectionChanged;
        }
        finally
        {
            _isSynchronizing = false;
            IsRuleDirty = false;
            SelectedRule.IsDirty = false;
        }
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
        IRuleValidator? ruleValidator = null,
        IFolderPathNormalizer? pathNormalizer = null,
        IMonitoredFolderCatalog? folderCatalog = null,
        IRuleSerializationService? ruleSerializationService = null)
    {
        _ruleRepository = ruleRepository;
        _importExportService = importExportService;
        _serviceProvider = serviceProvider;
        _ruleSerializationService = ruleSerializationService
            ?? (IRuleSerializationService?)serviceProvider.GetService(typeof(IRuleSerializationService))
            ?? new RuleSerializationService();

        _ruleSummaryGenerator = ruleSummaryGenerator
            ?? (IRuleSummaryGenerator?)serviceProvider.GetService(typeof(IRuleSummaryGenerator))
            ?? new RuleSummaryGenerator();

        _ruleValidator = ruleValidator
            ?? (IRuleValidator?)serviceProvider.GetService(typeof(IRuleValidator))
            ?? new RuleValidator();

        _pathNormalizer = pathNormalizer
            ?? (IFolderPathNormalizer?)serviceProvider.GetService(typeof(IFolderPathNormalizer))
            ?? new FolderPathNormalizer();

        _folderCatalog = folderCatalog
            ?? (IMonitoredFolderCatalog?)serviceProvider.GetService(typeof(IMonitoredFolderCatalog));

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

        if (_folderCatalog != null)
        {
            _folderCatalog.CatalogChanged += (s, e) => UpdateHealthAlerts();
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
            ?? new RuleWizardViewModel(_ruleRepository, _ruleValidator, executionPlanGen, _ruleSummaryGenerator, _pathNormalizer);

        WizardViewModel.SaveHandler = SaveDraftAsync;
        WizardViewModel.CancelHandler = HandleWizardCancel;

        WizardViewModel.RuleSaved += async (s, savedRule) =>
        {
            await LoadRulesAsync();
            SelectedRule = Rules.FirstOrDefault(r => r.Id == savedRule.Id) ?? savedRule;
            IsRuleDirty = false;
            StatusMessage = $"Rule '{savedRule.Name}' saved successfully";
        };

        WizardViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(RuleWizardViewModel.RuleName)
                or nameof(RuleWizardViewModel.RuleDescription)
                or nameof(RuleWizardViewModel.IsRuleEnabled))
            {
                MarkRuleDirty();
            }
        };

        WizardViewModel.MonitoredFolders.CollectionChanged += (s, e) => MarkRuleDirty();

        ActionEditor.Actions.CollectionChanged += (s, e) => MarkRuleDirty();

        FoldersViewModel = (FoldersViewModel?)serviceProvider.GetService(typeof(FoldersViewModel))
            ?? new FoldersViewModel(_ruleRepository, _healthMonitor, _folderCatalog, _pathNormalizer);

        FoldersViewModel.RequestEditRuleCallback = ruleId =>
        {
            if (!ConfirmDiscardRuleEdits()) return;
            CurrentView = NavigationView.RuleEditor;
            var targetRule = Rules.FirstOrDefault(r => r.Id == ruleId);
            if (targetRule != null)
            {
                SelectedRule = targetRule;
            }
        };

        PreviewViewModel = (PreviewViewModel?)serviceProvider.GetService(typeof(PreviewViewModel))
            ?? new PreviewViewModel(
                executionPlanGen,
                (IActionExecutor?)serviceProvider.GetService(typeof(IActionExecutor))
                    ?? new ActionExecutor((IVariableEngine?)serviceProvider.GetService(typeof(IVariableEngine)) ?? new VariableEngine()),
                (IExecutionDiscrepancyAnalyzer?)serviceProvider.GetService(typeof(IExecutionDiscrepancyAnalyzer)),
                (IBatchProcessingCoordinator?)serviceProvider.GetService(typeof(IBatchProcessingCoordinator)),
                (IExplainableDecisionEngine?)serviceProvider.GetService(typeof(IExplainableDecisionEngine)) ?? new ExplainableDecisionEngine());

        PreviewViewModel.EnablePreFlightValidation = true;

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
        if (_healthMonitor == null && _folderCatalog == null) return;

        var alertSummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_healthMonitor != null)
        {
            var allHealth = _healthMonitor.GetAllFolderHealth();
            var warningFolders = allHealth.Values
                .Where(h => h.Status == Services.Monitoring.WatcherHealthStatus.Degraded 
                         || h.Status == Services.Monitoring.WatcherHealthStatus.Disconnected 
                         || h.Status == Services.Monitoring.WatcherHealthStatus.Error)
                .ToList();

            foreach (var w in warningFolders)
            {
                var key = _pathNormalizer.TryNormalize(w.Path, out var norm, out _) ? norm : w.Path;
                var displayName = Path.GetFileName(key) is { Length: > 0 } n ? n : key;
                alertSummaries[key] = $"{displayName}: {w.Status} ({w.StatusDetail ?? "Check folder access"})";
            }
        }

        if (_folderCatalog != null)
        {
            foreach (var f in _folderCatalog.Folders.Where(x => x.IsAnyRuleEnabled && x.HasAlert))
            {
                if (!alertSummaries.ContainsKey(f.NormalizedPath))
                {
                    var displayName = Path.GetFileName(f.NormalizedPath) is { Length: > 0 } n ? n : f.NormalizedPath;
                    alertSummaries[f.NormalizedPath] = $"{displayName}: {f.HealthSummary} (Rules: {f.AssociatedRulesSummary})";
                }
            }
        }

        AlertCount = alertSummaries.Count;
        _activeAlertSummary = alertSummaries.Count > 0
            ? string.Join("; ", alertSummaries.Values)
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
        ApplicationLifecycleState.Idle => TidyUp.Styles.ThemeTokens.NeutralBackground,
        ApplicationLifecycleState.ConfiguringRule => TidyUp.Styles.ThemeTokens.WarningBackground,
        ApplicationLifecycleState.Simulating => TidyUp.Styles.ThemeTokens.InfoBackground,
        ApplicationLifecycleState.PreviewReady => TidyUp.Styles.ThemeTokens.SuccessBackground,
        ApplicationLifecycleState.ExecutingBatch => TidyUp.Styles.ThemeTokens.ErrorBackground,
        ApplicationLifecycleState.MonitoringRunning => AlertCount > 0 ? TidyUp.Styles.ThemeTokens.WarningBackground : TidyUp.Styles.ThemeTokens.SuccessBackground,
        ApplicationLifecycleState.MonitoringPaused => TidyUp.Styles.ThemeTokens.WarningBackground,
        _ => TidyUp.Styles.ThemeTokens.NeutralBackground
    };

    public string StateBadgeForeground => StateManager.CurrentState switch
    {
        ApplicationLifecycleState.Idle => TidyUp.Styles.ThemeTokens.Neutral,
        ApplicationLifecycleState.ConfiguringRule => TidyUp.Styles.ThemeTokens.Warning,
        ApplicationLifecycleState.Simulating => TidyUp.Styles.ThemeTokens.Info,
        ApplicationLifecycleState.PreviewReady => TidyUp.Styles.ThemeTokens.Success,
        ApplicationLifecycleState.ExecutingBatch => TidyUp.Styles.ThemeTokens.Error,
        ApplicationLifecycleState.MonitoringRunning => AlertCount > 0 ? TidyUp.Styles.ThemeTokens.Warning : TidyUp.Styles.ThemeTokens.Success,
        ApplicationLifecycleState.MonitoringPaused => TidyUp.Styles.ThemeTokens.Warning,
        _ => TidyUp.Styles.ThemeTokens.Neutral
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
                CanonicalizeRuleFolders(rule);
                UpdateRuleMetadata(rule);
                Rules.Add(rule);
            }
            FilterRules();
            await DashboardViewModel.LoadDashboardDataAsync();
            await FoldersViewModel.LoadFoldersAsync();
            UpdateHealthAlerts();
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

    private void CanonicalizeRuleFolders(Rule rule)
    {
        if (rule?.MonitoredFolders == null || rule.MonitoredFolders.Count == 0)
            return;

        var deduplicated = new List<MonitoredFolder>();
        foreach (var folder in rule.MonitoredFolders)
        {
            if (string.IsNullOrWhiteSpace(folder.Path))
                continue;

            folder.Path = _pathNormalizer.TryNormalize(folder.Path, out var canonical, out _)
                ? canonical
                : folder.Path.Trim();

            var existing = deduplicated.FirstOrDefault(f => _pathNormalizer.AreEqual(f.Path, folder.Path));
            if (existing == null)
            {
                deduplicated.Add(folder);
            }
            else
            {
                if (folder.IncludeSubfolders)
                {
                    existing.IncludeSubfolders = true;
                }

                foreach (var excl in folder.ExclusionPatterns)
                {
                    if (!existing.ExclusionPatterns.Any(p => string.Equals(p, excl, StringComparison.OrdinalIgnoreCase)))
                    {
                        existing.ExclusionPatterns.Add(excl);
                    }
                }
            }
        }

        if (deduplicated.Count != rule.MonitoredFolders.Count)
        {
            rule.MonitoredFolders.Clear();
            foreach (var item in deduplicated)
            {
                rule.MonitoredFolders.Add(item);
            }
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
        _activeRuleDraft = new RuleDraft(newRule, isNew: true);
        IsRuleDirty = true;
        newRule.IsDirty = true;
        StatusMessage = "Creating new rule in Guided Workflow (Disabled)";
    }

    private bool CanCreateNewRule() => StateManager.CurrentState != ApplicationLifecycleState.ExecutingBatch;

    /// <summary>
    /// Saves the selected rule to the database, maintaining parity across Guided and Advanced editors.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveRule))]
    public async Task<bool> SaveRuleAsync()
    {
        return await SaveDraftAsync();
    }

    /// <summary>
    /// Core draft persistence method shared by Shell Save and Wizard Save.
    /// Preserves all metadata, updates baseline on success, and retains dirty state on failure.
    /// </summary>
    public async Task<bool> SaveDraftAsync()
    {
        if (SelectedRule == null) return false;

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

            CanonicalizeRuleFolders(SelectedRule);

            var existing = await _ruleRepository.GetByIdAsync(SelectedRule.Id);
            if (existing == null)
            {
                await _ruleRepository.AddAsync(SelectedRule);
                if (!Rules.Any(r => r.Id == SelectedRule.Id))
                {
                    Rules.Add(SelectedRule);
                }
                StatusMessage = "Rule created successfully";
            }
            else
            {
                await _ruleRepository.UpdateAsync(SelectedRule);
                StatusMessage = "Rule updated successfully";
            }

            // SUCCESS PATH ONLY:
            _activeRuleDraft?.CommitBaseline(SelectedRule);
            IsRuleDirty = false;
            SelectedRule.IsDirty = false;
            UpdateRuleMetadata(SelectedRule);
            FilterRules();
            StateManager.NotifyRuleSaved();
            await DashboardViewModel.LoadDashboardDataAsync();
            await FoldersViewModel.LoadFoldersAsync();
            UpdateHealthAlerts();
            WizardViewModel.ErrorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving rule: {ex.Message}";
            WizardViewModel.ErrorMessage = $"Failed to save rule: {ex.Message}";
            // Retain dirty state on error!
            return false;
        }
        finally
        {
            IsLoading = false;
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
            await DashboardViewModel.LoadDashboardDataAsync();
            await FoldersViewModel.LoadFoldersAsync();
            UpdateHealthAlerts();
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

        StateManager.TransitionTo(ApplicationLifecycleState.Simulating, "Testing rule preview");

        try
        {
            CurrentView = NavigationView.PreviewReview;
            await PreviewViewModel.SimulateRuleAsync(SelectedRule);
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
    /// <summary>
    /// Gets or sets a callback for confirming rule imports.
    /// When null, defaults to showing a standard MessageBox.
    /// </summary>
    public Func<string, string, bool>? ConfirmImportCallback { get; set; }

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
                var json = _ruleSerializationService.ExportRules(new[] { SelectedRule });
                await File.WriteAllTextAsync(dialog.FileName, json);
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
                var json = _ruleSerializationService.ExportRules(Rules);
                await File.WriteAllTextAsync(dialog.FileName, json);
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
    /// Imports rules from raw JSON string, validating schema, enforcing disabled state, and atomically saving to repository.
    /// </summary>
    public async Task<bool> ImportRulesFromJsonAsync(string jsonContent, string? sourceFileName = null)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            StatusMessage = "Import failed: Package JSON is empty or whitespace.";
            return false;
        }

        IsLoading = true;
        StatusMessage = "Validating rules package...";

        try
        {
            var validation = _ruleSerializationService.ValidatePackageSchema(jsonContent);
            if (!validation.IsValid)
            {
                StatusMessage = $"Import failed: {validation.ErrorMessage}";
                return false;
            }

            var importResult = _ruleSerializationService.ImportRules(jsonContent);
            if (!importResult.Success || importResult.ImportedRules.Count == 0)
            {
                StatusMessage = $"Import failed: {importResult.ErrorMessage ?? "No rules found in package."}";
                return false;
            }

            var ruleList = importResult.ImportedRules;

            // Invariant: All imported rules MUST be disabled
            foreach (var r in ruleList)
            {
                r.IsEnabled = false;
            }

            var displayName = !string.IsNullOrWhiteSpace(sourceFileName) ? Path.GetFileName(sourceFileName) : "file";
            var confirmMsg =
                $"Import {ruleList.Count} rule(s) from {displayName}?\n\n" +
                $"Rules to import (will be imported as Disabled):\n" +
                string.Join("\n", ruleList.Take(5).Select(r => $"  • {r.Name}")) +
                (ruleList.Count > 5 ? $"\n  ... and {ruleList.Count - 5} more" : "");

            bool proceed = ConfirmImportCallback != null
                ? ConfirmImportCallback(confirmMsg, "Confirm Import")
                : MessageBox.Show(confirmMsg, "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

            if (!proceed)
            {
                StatusMessage = "Import cancelled";
                return false;
            }

            // Assign execution orders sequentially after current highest priority
            int currentMaxOrder = Rules.Any() ? Rules.Max(r => r.ExecutionOrder) : -1;
            for (int i = 0; i < ruleList.Count; i++)
            {
                ruleList[i].ExecutionOrder = currentMaxOrder + 1 + i;
            }

            // Atomic batch commit: all or nothing
            var persistedRules = await _ruleRepository.AddRangeAsync(ruleList);

            foreach (var rule in persistedRules)
            {
                UpdateRuleMetadata(rule);
                Rules.Add(rule);
            }

            FilterRules();

            StatusMessage = $"Imported {persistedRules.Count} rule(s) from {displayName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error importing rules: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

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
            try
            {
                var fileContent = await File.ReadAllTextAsync(dialog.FileName);
                await ImportRulesFromJsonAsync(fileContent, dialog.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reading import file: {ex.Message}";
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

        if (value == NavigationView.Folders)
        {
            _ = FoldersViewModel.LoadFoldersAsync();
        }
        else if (value == NavigationView.PreviewReview && PreviewViewModel.ExecutionPlan == null && SelectedRule != null)
        {
            _ = PreviewViewModel.SimulateRuleAsync(SelectedRule);
        }
    }

    public bool IsDashboardActive => CurrentView == NavigationView.Dashboard;
    public bool IsRulesActive => CurrentView == NavigationView.RuleEditor;
    public bool IsFoldersActive => CurrentView == NavigationView.Folders;
    public bool IsPreviewActive => CurrentView == NavigationView.PreviewReview;
    public bool IsHistoryActive => CurrentView == NavigationView.History;
    public bool IsSettingsActive => CurrentView == NavigationView.Settings;

    private void MarkRuleDirty()
    {
        if (_isSynchronizing || SelectedRule == null) return;
        IsRuleDirty = true;
        SelectedRule.IsDirty = true;
        UpdateRuleMetadata(SelectedRule);
        StateManager.NotifyRuleModified(SelectedRule?.Name);
        SaveRuleCommand.NotifyCanExecuteChanged();
        TestRuleCommand.NotifyCanExecuteChanged();
    }

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

        // These only change how the rule is described (e.g. the list subtitle), not the rule itself.
        if (e.PropertyName is nameof(Rule.FilesProcessedCount) or nameof(Rule.LastRunDate))
        {
            return;
        }

        MarkRuleDirty();

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
        MarkRuleDirty();
    }
}
