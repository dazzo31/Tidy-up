using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Services.Folders;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Services.Validation;

namespace TidyUp.ViewModels.RuleEditor;

public enum WizardStage
{
    Folders = 1,
    Conditions = 2,
    Actions = 3,
    Review = 4
}

/// <summary>
/// ViewModel coordinating the guided 4-stage rule creation wizard:
/// Stage 1: Monitored Folders
/// Stage 2: Conditions
/// Stage 3: Actions
/// Stage 4: Review & Dry-Run Simulation
/// </summary>
public partial class RuleWizardViewModel : ObservableObject
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IRuleValidator _ruleValidator;
    private readonly IExecutionPlanGenerator _executionPlanGenerator;
    private readonly IRuleSummaryGenerator _ruleSummaryGenerator;
    private readonly IFolderPathNormalizer _pathNormalizer;

    public event Action? RequestClose;
    public event EventHandler<Rule>? RuleSaved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(IsReviewStage))]
    [NotifyPropertyChangedFor(nameof(StageTitle))]
    private WizardStage _currentStage = WizardStage.Folders;

    [ObservableProperty]
    private string _ruleName = string.Empty;

    [ObservableProperty]
    private string _ruleDescription = string.Empty;

    [ObservableProperty]
    private Guid? _editingRuleId;

    [ObservableProperty]
    private bool _isRuleEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddFolderCommand))]
    private string _newFolderPathInput = string.Empty;

    public ObservableCollection<MonitoredFolder> MonitoredFolders { get; } = new();

    public ConditionEditorViewModel ConditionEditor { get; } = new();

    public ActionEditorViewModel ActionEditor { get; } = new();

    [ObservableProperty]
    private RuleValidationResult? _validationResult;

    [ObservableProperty]
    private ExecutionPlan? _executionPlan;

    [ObservableProperty]
    private bool _isDryRunning;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _ruleNaturalLanguageSummary = string.Empty;

    public bool CanGoPrevious => CurrentStage > WizardStage.Folders;
    public bool IsReviewStage => CurrentStage == WizardStage.Review;

    public string StageTitle => CurrentStage switch
    {
        WizardStage.Folders => "Step 1: Monitored Folders",
        WizardStage.Conditions => "Step 2: File Conditions",
        WizardStage.Actions => "Step 3: Target Actions",
        WizardStage.Review => "Step 4: Review & Dry Run",
        _ => "Rule Wizard"
    };

    public Rule? CreatedRule { get; private set; }

    public RuleWizardViewModel(
        IRuleRepository ruleRepository,
        IRuleValidator ruleValidator,
        IExecutionPlanGenerator executionPlanGenerator,
        IRuleSummaryGenerator? ruleSummaryGenerator = null,
        IFolderPathNormalizer? pathNormalizer = null)
    {
        _ruleRepository = ruleRepository;
        _ruleValidator = ruleValidator;
        _executionPlanGenerator = executionPlanGenerator;
        _ruleSummaryGenerator = ruleSummaryGenerator ?? new RuleSummaryGenerator();
        _pathNormalizer = pathNormalizer ?? new FolderPathNormalizer();

        // Initialize default empty root condition
        ConditionEditor.RootCondition = new ConditionGroup { Operator = Models.Enums.LogicOperator.And };
    }

    public bool CanAddFolder(string? path) => !string.IsNullOrWhiteSpace(path) || !string.IsNullOrWhiteSpace(NewFolderPathInput);

    /// <summary>
    /// Adds a monitored folder either from parameter or from text input.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddFolder))]
    public void AddFolder(string? path = null)
    {
        var rawPath = !string.IsNullOrWhiteSpace(path) ? path : NewFolderPathInput?.Trim();
        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        var targetPath = _pathNormalizer.TryNormalize(rawPath, out var normalized, out _)
            ? normalized
            : rawPath.Trim();

        if (MonitoredFolders.Any(f => _pathNormalizer.AreEqual(f.Path, targetPath)))
        {
            ErrorMessage = $"Folder '{targetPath}' is already in the list.";
            return;
        }

        MonitoredFolders.Add(new MonitoredFolder
        {
            Path = targetPath,
            IncludeSubfolders = true
        });

        NewFolderPathInput = string.Empty;
        ErrorMessage = null;
    }

    /// <summary>
    /// Opens the Windows folder picker dialog to select a folder.
    /// </summary>
    [RelayCommand]
    public void BrowseAndAddFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Monitored Folder",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            AddFolder(dialog.FolderName);
        }
    }

    /// <summary>
    /// Removes a monitored folder from the list.
    /// </summary>
    [RelayCommand]
    public void RemoveFolder(MonitoredFolder? folder)
    {
        if (folder != null)
        {
            MonitoredFolders.Remove(folder);
        }
    }

    /// <summary>
    /// Advances to the next stage after validating the current stage.
    /// </summary>
    [RelayCommand]
    public void NextStage()
    {
        ErrorMessage = null;

        switch (CurrentStage)
        {
            case WizardStage.Folders:
                if (string.IsNullOrWhiteSpace(RuleName))
                {
                    ErrorMessage = "Please specify a name for this rule.";
                    return;
                }
                if (MonitoredFolders.Count == 0)
                {
                    ErrorMessage = "Please add at least one monitored folder.";
                    return;
                }
                ConditionEditor.SetMonitoredFolders(MonitoredFolders.ToList());
                CurrentStage = WizardStage.Conditions;
                break;

            case WizardStage.Conditions:
                CurrentStage = WizardStage.Actions;
                break;

            case WizardStage.Actions:
                if (ActionEditor.Actions.Count == 0)
                {
                    ErrorMessage = "Please add at least one action to execute.";
                    return;
                }
                PrepareReviewStage();
                CurrentStage = WizardStage.Review;
                break;

            case WizardStage.Review:
                // Already at final stage
                break;
        }
    }

    /// <summary>
    /// Navigates to the previous stage.
    /// </summary>
    [RelayCommand]
    public void PreviousStage()
    {
        ErrorMessage = null;
        if (CurrentStage > WizardStage.Folders)
        {
            CurrentStage = (WizardStage)((int)CurrentStage - 1);
        }
    }

    /// <summary>
    /// Loads an existing Rule into the wizard for guided editing.
    /// </summary>
    public void LoadFromRule(Rule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        EditingRuleId = rule.Id;
        RuleName = rule.Name ?? string.Empty;
        RuleDescription = rule.Description ?? string.Empty;
        IsRuleEnabled = rule.IsEnabled;

        MonitoredFolders.Clear();
        foreach (var folder in rule.MonitoredFolders)
        {
            MonitoredFolders.Add(new MonitoredFolder
            {
                Path = folder.Path,
                IncludeSubfolders = folder.IncludeSubfolders,
                ExclusionPatterns = new ObservableCollection<string>(folder.ExclusionPatterns)
            });
        }

        ConditionEditor.RootCondition = rule.Conditions ?? new ConditionGroup { Operator = Models.Enums.LogicOperator.And };
        ConditionEditor.SetMonitoredFolders(MonitoredFolders.ToList());

        ActionEditor.Actions.Clear();
        foreach (var action in rule.Actions.OrderBy(a => a.Order))
        {
            ActionEditor.Actions.Add(action);
        }

        CurrentStage = WizardStage.Folders;
        PrepareReviewStage();
    }

    /// <summary>
    /// Updates the specified rule entity with current wizard configuration.
    /// </summary>
    public void SyncToRule(Rule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        rule.Name = RuleName.Trim();
        rule.Description = RuleDescription.Trim();
        rule.IsEnabled = IsRuleEnabled;

        rule.MonitoredFolders.Clear();
        foreach (var folder in MonitoredFolders)
        {
            rule.MonitoredFolders.Add(new MonitoredFolder
            {
                Path = folder.Path,
                IncludeSubfolders = folder.IncludeSubfolders,
                ExclusionPatterns = new ObservableCollection<string>(folder.ExclusionPatterns)
            });
        }

        rule.Conditions = ConditionEditor.RootCondition;

        rule.Actions.Clear();
        foreach (var action in ActionEditor.Actions)
        {
            rule.Actions.Add(action);
        }
    }

    /// <summary>
    /// Resets the wizard to clean initial state.
    /// </summary>
    public void ResetWizard()
    {
        EditingRuleId = null;
        RuleName = string.Empty;
        RuleDescription = string.Empty;
        IsRuleEnabled = false;
        MonitoredFolders.Clear();
        ConditionEditor.RootCondition = new ConditionGroup { Operator = Models.Enums.LogicOperator.And };
        ActionEditor.Actions.Clear();
        CurrentStage = WizardStage.Folders;
        ValidationResult = null;
        ExecutionPlan = null;
        ErrorMessage = null;
        CreatedRule = null;
    }

    /// <summary>
    /// Toggles the rule enabled state.
    /// </summary>
    [RelayCommand]
    public void ToggleRuleEnabled()
    {
        IsRuleEnabled = !IsRuleEnabled;
    }

    /// <summary>
    /// Builds the domain Rule object representation from current wizard settings.
    /// </summary>
    public Rule BuildRule()
    {
        var rule = new Rule
        {
            Id = EditingRuleId ?? Guid.NewGuid(),
            Name = RuleName.Trim(),
            Description = RuleDescription.Trim(),
            IsEnabled = IsRuleEnabled,
            Conditions = ConditionEditor.RootCondition
        };

        foreach (var folder in MonitoredFolders)
        {
            rule.MonitoredFolders.Add(new MonitoredFolder
            {
                Path = folder.Path,
                IncludeSubfolders = folder.IncludeSubfolders,
                ExclusionPatterns = new ObservableCollection<string>(folder.ExclusionPatterns)
            });
        }

        foreach (var action in ActionEditor.Actions)
        {
            rule.Actions.Add(action);
        }

        return rule;
    }

    /// <summary>
    /// Prepares Stage 4: runs static rule safety validation and prepares simulation summary.
    /// </summary>
    public void PrepareReviewStage()
    {
        var rule = BuildRule();
        ValidationResult = _ruleValidator.ValidateRule(rule);
        RuleNaturalLanguageSummary = _ruleSummaryGenerator.GenerateSummary(rule);
        ExecutionPlan = null;
    }

    /// <summary>
    /// Executes a dry-run simulation of the rule without modifying any files.
    /// </summary>
    [RelayCommand]
    public async Task RunDryRunAsync()
    {
        IsDryRunning = true;
        ErrorMessage = null;

        try
        {
            var rule = BuildRule();
            ExecutionPlan = await _executionPlanGenerator.GeneratePlanAsync(rule);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Dry run simulation failed: {ex.Message}";
        }
        finally
        {
            IsDryRunning = false;
        }
    }

    /// <summary>
    /// Saves the configured rule to SQLite and notifies completion.
    /// </summary>
    [RelayCommand]
    public async Task SaveRuleAsync()
    {
        ErrorMessage = null;
        IsSaving = true;

        try
        {
            var rule = BuildRule();
            var validation = _ruleValidator.ValidateRule(rule);
            if (!validation.IsValid)
            {
                ErrorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                return;
            }

            if (EditingRuleId.HasValue && EditingRuleId.Value != Guid.Empty)
            {
                var existing = await _ruleRepository.GetByIdAsync(rule.Id);
                if (existing != null)
                {
                    await _ruleRepository.UpdateAsync(rule);
                }
                else
                {
                    await _ruleRepository.AddAsync(rule);
                }
            }
            else
            {
                await _ruleRepository.AddAsync(rule);
            }

            CreatedRule = rule;
            RuleSaved?.Invoke(this, rule);
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save rule: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Gets whether any unsaved changes exist in the wizard.
    /// </summary>
    public bool IsDirty =>
        !string.IsNullOrWhiteSpace(RuleName) ||
        !string.IsNullOrWhiteSpace(RuleDescription) ||
        MonitoredFolders.Count > 0 ||
        ActionEditor.Actions.Count > 0 ||
        (ConditionEditor.RootCondition != null && ConditionEditor.RootCondition.Conditions.Count > 0);

    /// <summary>
    /// Optional callback for prompt confirmation; defaults to MessageBox in UI.
    /// </summary>
    public Func<string, string, bool>? ConfirmDiscardCallback { get; set; }

    /// <summary>
    /// Cancels wizard and requests close, prompting if unsaved edits exist.
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        if (IsDirty && CreatedRule == null)
        {
            bool proceed = ConfirmDiscardCallback != null
                ? ConfirmDiscardCallback("You have unsaved changes in the rule creation wizard. Discard unsaved changes?", "Discard Changes")
                : System.Windows.MessageBox.Show(
                    "You have unsaved changes in the rule creation wizard. Discard unsaved changes?",
                    "Discard Changes",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;

            if (!proceed)
            {
                return;
            }
        }

        RequestClose?.Invoke();
    }
}

