using TidyUp.Models.Enums;

namespace TidyUp.Models.Domain;

/// <summary>
/// Represents a dry-run execution plan produced by simulating rules against target files.
/// Contains all planned file actions and aggregate operational statistics without modifying files.
/// </summary>
public class ExecutionPlan
{
    public Guid PlanId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when this simulation plan was generated.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID of the rule simulated.
    /// </summary>
    public Guid RuleId { get; set; }

    /// <summary>
    /// Name of the rule simulated.
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// List of all planned actions generated during simulation.
    /// </summary>
    public List<PlannedFileAction> PlannedActions { get; set; } = new();

    /// <summary>
    /// Total number of files scanned in target folders.
    /// </summary>
    public int TotalScanned { get; set; }

    /// <summary>
    /// Total number of files that matched rule conditions.
    /// </summary>
    public int TotalMatched { get; set; }

    public int MoveCount => PlannedActions.Count(a => a.ActionType == ActionType.Move);
    public int CopyCount => PlannedActions.Count(a => a.ActionType == ActionType.Copy);
    public int RenameCount => PlannedActions.Count(a => a.ActionType == ActionType.Rename);
    public int ChangeExtensionCount => PlannedActions.Count(a => a.ActionType == ActionType.ChangeExtension);
    public int DeleteCount => PlannedActions.Count(a => a.ActionType == ActionType.Delete);
    public int ExtractCount => PlannedActions.Count(a => a.ActionType == ActionType.ExtractArchive);
    public int RunCommandCount => PlannedActions.Count(a => a.ActionType == ActionType.RunCommand);

    /// <summary>
    /// Count of actions that face destination collisions or conflicts.
    /// </summary>
    public int ConflictCount => PlannedActions.Count(a => a.HasConflict);

    /// <summary>
    /// Indicates whether any actions in this plan face conflicts.
    /// </summary>
    public bool HasConflicts => ConflictCount > 0;
}

