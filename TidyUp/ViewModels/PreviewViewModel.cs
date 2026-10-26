using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Diagnostics;
using TidyUp.Services.Processing;
using TidyUp.Services.Simulation;
using TidyUp.ViewModels.Dialogs;

namespace TidyUp.ViewModels;

/// <summary>
/// Presentation wrapper for a single planned file action in the Side-by-Side Impact Preview.
/// </summary>
public partial class PlannedActionItemViewModel : ObservableObject
{
    public PlannedFileAction PlannedAction { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBeApplied))]
    private bool _isApproved;

    [ObservableProperty]
    private string _executionStatus = "Pending";

    [ObservableProperty]
    private bool _isExecuted;

    [ObservableProperty]
    private bool _executionFailed;

    public PlannedActionItemViewModel(PlannedFileAction plannedAction)
    {
        PlannedAction = plannedAction;
        // Default to approved only if there are no conflicts and not skipped
        _isApproved = !plannedAction.HasConflict && !plannedAction.IsSkipped;
    }

    public Guid Id => PlannedAction.Id;
    public string SourcePath => PlannedAction.SourcePath;
    public string InitialSourcePath => PlannedAction.InitialSourcePath;
    public Guid? DependsOnActionId => PlannedAction.DependsOnActionId;
    public string? TargetPath => PlannedAction.TargetPath;
    public bool IsSkipped => PlannedAction.IsSkipped;
    public string? ResolutionNote => PlannedAction.ResolutionNote;
    public ActionType ActionType => PlannedAction.ActionType;
    public FileAction FileAction => PlannedAction.FileAction;
    public string RuleName => PlannedAction.RuleName;
    public long FileSizeBytes => PlannedAction.FileSizeBytes;
    public DateTime SourceModifiedDate => PlannedAction.SourceModifiedDate;
    public bool HasConflict => PlannedAction.HasConflict;
    public string? ConflictDescription => PlannedAction.ConflictDescription;
    public ConflictResolution ConflictResolution => PlannedAction.ConflictResolution;

    public string Extension
    {
        get
        {
            var ext = Path.GetExtension(SourcePath);
            return string.IsNullOrEmpty(ext) ? "(none)" : ext.ToLowerInvariant();
        }
    }

    public string ActionBadgeText => ActionType switch
    {
        ActionType.Move => "MOVE",
        ActionType.Copy => "COPY",
        ActionType.Rename => "RENAME",
        ActionType.ChangeExtension => "CHANGE EXT",
        ActionType.Delete => "DELETE",
        ActionType.ExtractArchive => "EXTRACT",
        ActionType.RunCommand => "COMMAND",
        _ => ActionType.ToString().ToUpperInvariant()
    };

    public string ActionBadgeBackground => ActionType switch
    {
        ActionType.Move => TidyUp.Styles.ThemeTokens.InfoBackground,
        ActionType.Copy => TidyUp.Styles.ThemeTokens.TealBackground,
        ActionType.Rename => TidyUp.Styles.ThemeTokens.PurpleBackground,
        ActionType.ChangeExtension => TidyUp.Styles.ThemeTokens.PurpleBackground,
        ActionType.Delete => TidyUp.Styles.ThemeTokens.ErrorBackground,
        ActionType.ExtractArchive => TidyUp.Styles.ThemeTokens.NeutralBackground,
        ActionType.RunCommand => TidyUp.Styles.ThemeTokens.NeutralBackground,
        _ => TidyUp.Styles.ThemeTokens.NeutralBackground
    };

    public string ActionBadgeForeground => ActionType switch
    {
        ActionType.Move => TidyUp.Styles.ThemeTokens.Info,
        ActionType.Copy => TidyUp.Styles.ThemeTokens.Teal,
        ActionType.Rename => TidyUp.Styles.ThemeTokens.Purple,
        ActionType.ChangeExtension => TidyUp.Styles.ThemeTokens.Purple,
        ActionType.Delete => TidyUp.Styles.ThemeTokens.Error,
        ActionType.ExtractArchive => TidyUp.Styles.ThemeTokens.Neutral,
        ActionType.RunCommand => TidyUp.Styles.ThemeTokens.Neutral,
        _ => TidyUp.Styles.ThemeTokens.Neutral
    };

    public string StatusBadgeText
    {
        get
        {
            if (IsExecuted) return ExecutionFailed ? "FAILED" : "APPLIED";
            if (HasConflict) return "CONFLICT";
            return "READY";
        }
    }

    public string StatusBadgeBackground
    {
        get
        {
            if (IsExecuted) return ExecutionFailed ? TidyUp.Styles.ThemeTokens.ErrorBackground : TidyUp.Styles.ThemeTokens.SuccessBackground;
            if (HasConflict) return TidyUp.Styles.ThemeTokens.WarningBackground;
            return TidyUp.Styles.ThemeTokens.SuccessBackground;
        }
    }

    public string StatusBadgeForeground
    {
        get
        {
            if (IsExecuted) return ExecutionFailed ? TidyUp.Styles.ThemeTokens.Error : TidyUp.Styles.ThemeTokens.Success;
            if (HasConflict) return TidyUp.Styles.ThemeTokens.Warning;
            return TidyUp.Styles.ThemeTokens.Success;
        }
    }

    public bool CanBeApplied => IsApproved && !HasConflict && !IsExecuted;
}

/// <summary>
/// ViewModel for the Side-by-Side Impact Preview Window.
/// Displays planned actions generated by TASK-SAFE-01 dry-run simulation, provides
/// multi-criteria filtering, prevents execution when conflicts exist, and applies approved actions.
/// </summary>
public partial class PreviewViewModel : ObservableObject
{
    private readonly IExecutionPlanGenerator _planGenerator;
    private readonly IActionExecutor _actionExecutor;
    private readonly IExecutionDiscrepancyAnalyzer? _discrepancyAnalyzer;
    private readonly IBatchProcessingCoordinator _batchCoordinator;
    private readonly IExplainableDecisionEngine? _explainableEngine;
    private CancellationTokenSource? _batchCancellationTokenSource;

    [ObservableProperty]
    private Rule? _currentRule;

    [ObservableProperty]
    private EvaluationBreakdown? _selectedFileExplanation;

    [ObservableProperty]
    private bool _isExplainDrawerOpen;

    [ObservableProperty]
    private bool _showDeletionsOnly;

    public int MoveCount => AllItems.Count(i => i.ActionType == ActionType.Move);
    public int CopyCount => AllItems.Count(i => i.ActionType == ActionType.Copy);
    public int RenameCount => AllItems.Count(i => i.ActionType == ActionType.Rename);
    public int DeleteCount => AllItems.Count(i => i.ActionType == ActionType.Delete);

    /// <summary>
    /// Delegate for requesting user confirmation before applying changes.
    /// Can be overridden in unit tests to test execution without UI message boxes.
    /// </summary>
    public Func<string, string, bool> ConfirmApplyHandler { get; set; }

    /// <summary>
    /// Delegate for presenting the Destructive Operation Safeguard modal when high-risk or large batch actions are detected.
    /// Can be overridden in unit tests to simulate user confirmation or cancellation without UI modals.
    /// </summary>
    public Func<SafeguardConfirmationViewModel, bool> SafeguardDialogHandler { get; set; }

    /// <summary>
    /// Delegate for displaying the post-execution discrepancy analyzer window when deviations occur.
    /// </summary>
    public Action<ExecutionDiscrepancyReport>? ShowDiscrepancySummaryHandler { get; set; }

    [ObservableProperty]
    private ExecutionDiscrepancyReport? _discrepancyReport;

    [ObservableProperty]
    private ObservableCollection<PlannedActionItemViewModel> _allItems = new();

    [ObservableProperty]
    private ObservableCollection<PlannedActionItemViewModel> _filteredItems = new();

    [ObservableProperty]
    private PlannedActionItemViewModel? _selectedItem;

    partial void OnSelectedItemChanged(PlannedActionItemViewModel? value)
    {
        if (value != null && _explainableEngine != null && CurrentRule != null)
        {
            SelectedFileExplanation = _explainableEngine.ExplainMatch(CurrentRule, value.SourcePath);
            IsExplainDrawerOpen = true;
        }
        else
        {
            SelectedFileExplanation = null;
        }
    }

    [RelayCommand]
    public void ToggleExplainDrawer()
    {
        IsExplainDrawerOpen = !IsExplainDrawerOpen;
    }

    [ObservableProperty]
    private ExecutionPlan? _executionPlan;

    [ObservableProperty]
    private bool _hasScanWarnings;

    [ObservableProperty]
    private string? _scanWarningMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyChanges))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyChanges))]
    [NotifyPropertyChangedFor(nameof(CanCancelExecution))]
    private bool _isExecuting;

    public bool CanCancelExecution => IsExecuting && _batchCancellationTokenSource is { IsCancellationRequested: false };

    [ObservableProperty]
    private double _executionProgress;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // Summary statistics
    [ObservableProperty]
    private int _totalScannedFiles;

    [ObservableProperty]
    private int _totalMatchedFiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyChanges))]
    private int _conflictCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyChanges))]
    [NotifyPropertyChangedFor(nameof(ApplyButtonText))]
    private int _approvedCount;

    [ObservableProperty]
    private int _executedSuccessCount;

    [ObservableProperty]
    private int _executedFailureCount;

    // Filters
    [ObservableProperty]
    private ObservableCollection<string> _ruleFilterOptions = new() { "All Rules" };

    [ObservableProperty]
    private string _selectedRuleFilter = "All Rules";

    [ObservableProperty]
    private ObservableCollection<string> _extensionFilterOptions = new() { "All Extensions" };

    [ObservableProperty]
    private string _selectedExtensionFilter = "All Extensions";

    [ObservableProperty]
    private ObservableCollection<string> _actionTypeFilterOptions = new()
    {
        "All Actions",
        "Move",
        "Copy",
        "Rename",
        "ChangeExtension",
        "Delete",
        "ExtractArchive",
        "RunCommand"
    };

    [ObservableProperty]
    private string _selectedActionTypeFilter = "All Actions";

    [ObservableProperty]
    private bool _showConflictsOnly;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public bool CanApplyChanges =>
        !IsLoading &&
        !IsExecuting &&
        ExecutionPlan != null &&
        AllItems.Any(i => i.IsApproved) &&
        !AllItems.Any(i => i.IsApproved && i.HasConflict);

    /// <summary>
    /// Caption for the apply button. A bare "Apply 0" reads like a broken button, so the empty case is spelled out.
    /// </summary>
    public string ApplyButtonText => ApprovedCount switch
    {
        0 => "Apply approved changes (none selected)",
        1 => "Apply 1 approved change",
        _ => $"Apply {ApprovedCount} approved changes"
    };

    public bool EnablePreFlightValidation { get; set; }

    public PreviewViewModel(
        IExecutionPlanGenerator planGenerator,
        IActionExecutor actionExecutor,
        IExecutionDiscrepancyAnalyzer? discrepancyAnalyzer = null,
        IBatchProcessingCoordinator? batchCoordinator = null,
        IExplainableDecisionEngine? explainableEngine = null)
    {
        _planGenerator = planGenerator ?? throw new ArgumentNullException(nameof(planGenerator));
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _discrepancyAnalyzer = discrepancyAnalyzer;
        _batchCoordinator = batchCoordinator ?? new BatchProcessingCoordinator(_actionExecutor);
        _explainableEngine = explainableEngine;

        ConfirmApplyHandler = DefaultConfirmApply;
        SafeguardDialogHandler = DefaultSafeguardDialog;
        ShowDiscrepancySummaryHandler = DefaultShowDiscrepancySummary;
    }

    /// <summary>
    /// Cooperatively cancels the in-flight batch execution.
    /// Halts scheduling next files while waiting for the currently active file operation to finish safely.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelExecution))]
    public void CancelExecution()
    {
        if (IsExecuting && _batchCancellationTokenSource != null && !_batchCancellationTokenSource.IsCancellationRequested)
        {
            StatusMessage = "Cancelling batch... Waiting for in-flight operation to finish safely.";
            _batchCancellationTokenSource.Cancel();
            CancelExecutionCommand.NotifyCanExecuteChanged();
        }
    }

    private static bool DefaultConfirmApply(string message, string title)
    {
        return System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;
    }

    private static bool DefaultSafeguardDialog(SafeguardConfirmationViewModel vm)
    {
        var dialog = new Views.Dialogs.SafeguardConfirmationDialog(vm);
        var result = dialog.ShowDialog();
        return result == true && vm.IsConfirmed;
    }

    private static void DefaultShowDiscrepancySummary(ExecutionDiscrepancyReport report)
    {
        var vm = new ExecutionSummaryViewModel(report);
        var dialog = new Views.ExecutionSummaryWindow(vm);
        if (System.Windows.Application.Current?.MainWindow is { } owner)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();
    }

    /// <summary>
    /// Loads an already generated ExecutionPlan into the preview.
    /// </summary>
    public void LoadPlan(ExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        ExecutionPlan = plan;
        TotalScannedFiles = plan.TotalScanned;
        TotalMatchedFiles = plan.TotalMatched;
        ConflictCount = plan.ConflictCount;

        AllItems.Clear();
        foreach (var action in plan.PlannedActions)
        {
            var itemVm = new PlannedActionItemViewModel(action);
            itemVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PlannedActionItemViewModel.IsApproved))
                {
                    UpdateApprovedCount();
                    OnPropertyChanged(nameof(CanApplyChanges));
                    ApplyChangesCommand.NotifyCanExecuteChanged();
                }
            };
            AllItems.Add(itemVm);
        }

        PopulateFilterOptions();
        ApplyFilter();
        UpdateApprovedCount();

        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(CopyCount));
        OnPropertyChanged(nameof(RenameCount));
        OnPropertyChanged(nameof(DeleteCount));

        if (plan.IsIncomplete || plan.ScanErrors.Count > 0)
        {
            HasScanWarnings = true;
            ScanWarningMessage = "Scan partially completed. Some directories were inaccessible. Showing results for accessible files only.";
            StatusMessage = $"Scan partially completed ({plan.ScanErrors.Count} warning(s)). Showing results for accessible files only.";
        }
        else
        {
            HasScanWarnings = false;
            ScanWarningMessage = null;
            StatusMessage = ConflictCount > 0
                ? $"Plan generated: {AllItems.Count} planned action(s), {ConflictCount} conflict(s) detected."
                : $"Plan generated: {AllItems.Count} planned action(s). Ready to apply.";
        }

        OnPropertyChanged(nameof(CanApplyChanges));
        ApplyChangesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Runs a simulation for the specified rule and displays the resulting impact plan.
    /// </summary>
    private CancellationTokenSource? _simulationCts;
    private int _simulationGenerationId;

    [RelayCommand]
    public async Task SimulateRuleAsync(Rule rule)
    {
        if (rule == null)
        {
            StatusMessage = "No rule specified for preview.";
            return;
        }

        _simulationCts?.Cancel();
        _simulationCts?.Dispose();
        _simulationCts = new CancellationTokenSource();
        var token = _simulationCts.Token;
        int genId = Interlocked.Increment(ref _simulationGenerationId);

        CurrentRule = rule;
        IsLoading = true;
        StatusMessage = $"Simulating rule '{rule.Name}' across monitored folders...";

        try
        {
            var plan = await _planGenerator.GeneratePlanAsync(rule, token);
            if (genId == _simulationGenerationId && !token.IsCancellationRequested)
            {
                LoadPlan(plan);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by newer simulation
        }
        catch (Exception ex)
        {
            if (genId == _simulationGenerationId)
            {
                ExecutionPlan = null;
                StatusMessage = $"Simulation error: {ex.Message}";
            }
        }
        finally
        {
            if (genId == _simulationGenerationId)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(CanApplyChanges));
                ApplyChangesCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Applies all approved planned actions with user confirmation and progress tracking.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplyChanges))]
    public async Task ApplyChangesAsync()
    {
        var approvedItems = AllItems.Where(i => i.IsApproved && !i.HasConflict && !i.IsExecuted).ToList();
        if (approvedItems.Count == 0)
        {
            StatusMessage = "No approved actions to apply.";
            return;
        }

        if (EnablePreFlightValidation)
        {
            // Step 1: Pre-flight re-check against filesystem mutations
            var (preFlightSuccess, preFlightIssues) = PerformPreFlightCheck(approvedItems);
            if (!preFlightSuccess)
            {
                StatusMessage = $"Pre-flight validation failed: {preFlightIssues.Count} file(s) changed on disk. Please refresh preview.";
                ApplyFilter();
                return;
            }
        }

        // Step 1b: Verify partial scan acknowledgment if incomplete
        if (ExecutionPlan?.IsIncomplete == true)
        {
            var partialMsg = "The preview scan was partially completed because some directories were inaccessible or enumeration was interrupted.\n\nDo you want to proceed and apply changes for the accessible files only?";
            if (!ConfirmApplyHandler(partialMsg, "Partial Scan Warning"))
            {
                StatusMessage = "Apply cancelled: partial scan acknowledgment declined.";
                return;
            }
        }

        // Step 2: Evaluate destructive-action safeguards (permanent deletion, overwrite, >50 files)
        var safeguardVm = new SafeguardConfirmationViewModel();
        bool requiresSafeguard = safeguardVm.EvaluateRisks(approvedItems);
        if (requiresSafeguard)
        {
            if (!SafeguardDialogHandler(safeguardVm))
            {
                StatusMessage = "Safeguard check cancelled: no files were modified.";
                return;
            }
        }
        else
        {
            // Standard confirmation for non-destructive operations
            var confirmMsg = $"Are you sure you want to apply {approvedItems.Count} approved file action(s)?\n\nThis will execute file operations on disk.";
            if (!ConfirmApplyHandler(confirmMsg, "Confirm Apply Changes"))
            {
                StatusMessage = "Apply changes cancelled by user.";
                return;
            }
        }

        _batchCancellationTokenSource = new CancellationTokenSource();
        CancelExecutionCommand.NotifyCanExecuteChanged();

        IsExecuting = true;
        ExecutionProgress = 0;
        ExecutedSuccessCount = 0;
        ExecutedFailureCount = 0;
        StatusMessage = $"Applying changes: 0 of {approvedItems.Count}...";

        var batchId = Guid.NewGuid();
        var plannedActions = approvedItems.Select(i => i.PlannedAction).ToList();
        var itemMap = approvedItems.ToDictionary(i => i.PlannedAction.Id);

        var progressReporter = new Progress<BatchProgressReport>(report =>
        {
            ExecutionProgress = report.Percentage;
            if (report.CurrentAction != null && itemMap.TryGetValue(report.CurrentAction.Id, out var itemVm))
            {
                itemVm.IsExecuted = true;
                if (report.LastActionResult?.Success == true)
                {
                    itemVm.ExecutionFailed = false;
                    itemVm.ExecutionStatus = "Completed";
                }
                else
                {
                    itemVm.ExecutionFailed = true;
                    itemVm.ExecutionStatus = report.LastActionResult?.ErrorMessage ?? "Failed";
                }
            }
            StatusMessage = $"Applying changes: {report.CompletedCount} of {report.TotalCount}...";
        });

        try
        {
            var batchResult = await _batchCoordinator.ExecuteBatchAsync(
                plannedActions,
                batchId,
                progressReporter,
                _batchCancellationTokenSource.Token);

            ExecutedSuccessCount = batchResult.SucceededCount;
            ExecutedFailureCount = batchResult.FailedCount;

            // Update any cancelled / skipped items in viewmodel
            foreach (var skipped in batchResult.ItemResults.Where(r => r.Skipped))
            {
                if (itemMap.TryGetValue(skipped.Action.Id, out var skippedVm))
                {
                    skippedVm.IsExecuted = false;
                    skippedVm.ExecutionStatus = "Cancelled";
                }
            }

            if (batchResult.WasCancelled)
            {
                StatusMessage = $"Execution cancelled: {batchResult.SucceededCount} succeeded, {batchResult.CancelledOrSkippedCount} skipped/untouched, {batchResult.FailedCount} failed.";
            }
            else
            {
                StatusMessage = $"Execution complete: {ExecutedSuccessCount} succeeded, {ExecutedFailureCount} failed.";
            }

            if (_discrepancyAnalyzer != null)
            {
                var executedResults = approvedItems.Select(item =>
                    (item.PlannedAction, new ActionResult
                    {
                        Success = !item.ExecutionFailed && item.IsExecuted,
                        ErrorMessage = item.ExecutionFailed ? item.ExecutionStatus : (item.IsExecuted ? null : "Cancelled"),
                        ResultPath = item.TargetPath,
                        TargetPath = item.TargetPath
                    })).ToList();

                DiscrepancyReport = _discrepancyAnalyzer.AnalyzeFromResults(
                    approvedItems.Select(i => i.PlannedAction),
                    executedResults,
                    batchId);

                if (DiscrepancyReport.HasAnyDiscrepancies)
                {
                    ShowDiscrepancySummaryHandler?.Invoke(DiscrepancyReport);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Execution error: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
            _batchCancellationTokenSource?.Dispose();
            _batchCancellationTokenSource = null;
            CancelExecutionCommand.NotifyCanExecuteChanged();
            UpdateApprovedCount();
            OnPropertyChanged(nameof(CanApplyChanges));
            ApplyChangesCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Selects or deselects all unconflicted items in the current filtered view.
    /// </summary>
    [RelayCommand]
    public void SetAllApproved(bool approve)
    {
        foreach (var item in FilteredItems)
        {
            if (!item.HasConflict && !item.IsExecuted)
            {
                item.IsApproved = approve;
            }
        }
        UpdateApprovedCount();
        OnPropertyChanged(nameof(CanApplyChanges));
        ApplyChangesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public void ApproveAll() => SetAllApproved(true);

    [RelayCommand]
    public void RejectAll() => SetAllApproved(false);

    partial void OnSelectedRuleFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedExtensionFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedActionTypeFilterChanged(string value) => ApplyFilter();
    partial void OnShowConflictsOnlyChanged(bool value) => ApplyFilter();
    partial void OnShowDeletionsOnlyChanged(bool value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public (bool Success, List<string> Issues) PerformPreFlightCheck(IEnumerable<PlannedActionItemViewModel> items)
    {
        var issues = new List<string>();
        var approvedList = items.ToList();
        var approvedIds = new HashSet<Guid>(approvedList.Select(i => i.Id));

        foreach (var item in approvedList)
        {
            // If this action depends on a predecessor in a multi-action chain:
            if (item.DependsOnActionId.HasValue)
            {
                // Dependency closure: predecessor action MUST also be approved in the batch
                if (!approvedIds.Contains(item.DependsOnActionId.Value))
                {
                    issues.Add($"Action '{item.ActionType}' for '{Path.GetFileName(item.SourcePath)}' depends on an unselected preceding action.");
                    item.PlannedAction.HasConflict = true;
                    item.PlannedAction.ConflictDescription = "Dependent predecessor action is not selected";
                    continue;
                }

                // Verify the initial source file on disk
                var initialSource = string.IsNullOrEmpty(item.InitialSourcePath) ? item.SourcePath : item.InitialSourcePath;
                if (!File.Exists(initialSource))
                {
                    issues.Add($"Initial source file no longer exists: {initialSource}");
                    item.PlannedAction.HasConflict = true;
                    item.PlannedAction.ConflictDescription = "Initial file missing at pre-flight check";
                    continue;
                }

                try
                {
                    var fi = new FileInfo(initialSource);
                    if (Math.Abs((fi.LastWriteTime - item.SourceModifiedDate).TotalSeconds) > 2)
                    {
                        issues.Add($"File modified since preview: {initialSource}");
                    }
                }
                catch (Exception ex)
                {
                    issues.Add($"Cannot access initial source file: {initialSource} ({ex.Message})");
                }
            }
            else
            {
                // Root action in chain: source path must exist on disk
                if (!File.Exists(item.SourcePath))
                {
                    issues.Add($"File no longer exists: {item.SourcePath}");
                    item.PlannedAction.HasConflict = true;
                    item.PlannedAction.ConflictDescription = "File missing at pre-flight check";
                    continue;
                }

                try
                {
                    var fi = new FileInfo(item.SourcePath);
                    if (Math.Abs((fi.LastWriteTime - item.SourceModifiedDate).TotalSeconds) > 2)
                    {
                        issues.Add($"File modified since preview: {item.SourcePath}");
                    }
                }
                catch (Exception ex)
                {
                    issues.Add($"Cannot access file: {item.SourcePath} ({ex.Message})");
                }
            }
        }

        return (issues.Count == 0, issues);
    }

    public void ApplyFilter()
    {
        var query = AllItems.AsEnumerable();

        if (ShowConflictsOnly)
        {
            query = query.Where(i => i.HasConflict);
        }

        if (ShowDeletionsOnly)
        {
            query = query.Where(i => i.ActionType == ActionType.Delete);
        }

        if (!string.IsNullOrEmpty(SelectedRuleFilter) && SelectedRuleFilter != "All Rules")
        {
            query = query.Where(i => string.Equals(i.RuleName, SelectedRuleFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(SelectedExtensionFilter) && SelectedExtensionFilter != "All Extensions")
        {
            query = query.Where(i => string.Equals(i.Extension, SelectedExtensionFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(SelectedActionTypeFilter) && SelectedActionTypeFilter != "All Actions")
        {
            query = query.Where(i => string.Equals(i.ActionType.ToString(), SelectedActionTypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(i =>
                i.SourcePath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (i.TargetPath != null && i.TargetPath.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredItems = new ObservableCollection<PlannedActionItemViewModel>(query.ToList());
    }

    private void PopulateFilterOptions()
    {
        var rules = AllItems.Select(i => i.RuleName).Where(r => !string.IsNullOrEmpty(r)).Distinct().OrderBy(r => r).ToList();
        RuleFilterOptions = new ObservableCollection<string> { "All Rules" };
        foreach (var r in rules) RuleFilterOptions.Add(r);

        var extensions = AllItems.Select(i => i.Extension).Distinct().OrderBy(e => e).ToList();
        ExtensionFilterOptions = new ObservableCollection<string> { "All Extensions" };
        foreach (var ext in extensions) ExtensionFilterOptions.Add(ext);

        SelectedRuleFilter = "All Rules";
        SelectedExtensionFilter = "All Extensions";
        SelectedActionTypeFilter = "All Actions";
        ShowConflictsOnly = false;
        SearchText = string.Empty;
    }

    private void UpdateApprovedCount()
    {
        ApprovedCount = AllItems.Count(i => i.IsApproved && !i.IsExecuted);
    }
}

