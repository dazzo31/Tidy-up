using TidyUp.Models.Enums;

namespace TidyUp.Models.Domain;

/// <summary>
/// Represents an in-memory planned file action produced during rule simulation.
/// </summary>
public class PlannedFileAction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The source file path (may be an intermediate virtual path for chained actions).
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// The original real file path on disk before any preceding actions in the chain.
    /// </summary>
    public string InitialSourcePath { get; set; } = string.Empty;

    /// <summary>
    /// ID of the predecessor planned action that this action depends on in a multi-action chain, or null if independent.
    /// </summary>
    public Guid? DependsOnActionId { get; set; }

    /// <summary>
    /// The root monitored folder from which the source file was discovered, used for relative path calculations.
    /// </summary>
    public string? MonitoredSourceRoot { get; set; }

    /// <summary>
    /// The proposed destination or resulting file path, if applicable.
    /// </summary>
    public string? TargetPath { get; set; }

    /// <summary>
    /// The action type to be performed.
    /// </summary>
    public ActionType ActionType { get; set; }

    /// <summary>
    /// The original domain file action configuration.
    /// </summary>
    public FileAction FileAction { get; set; } = null!;

    /// <summary>
    /// Name of the rule generating this action.
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// ID of the rule generating this action.
    /// </summary>
    public Guid? RuleId { get; set; }

    /// <summary>
    /// Revision ID of the rule generating this action.
    /// </summary>
    public Guid? RuleRevisionId { get; set; }

    /// <summary>
    /// Size of the source file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Last modified date of the source file.
    /// </summary>
    public DateTime SourceModifiedDate { get; set; }

    /// <summary>
    /// Whether this action encounters an unresolvable conflict (e.g., target already exists under Prompt policy or batch collision).
    /// </summary>
    public bool HasConflict { get; set; }

    /// <summary>
    /// Description of the conflict if one exists.
    /// </summary>
    public string? ConflictDescription { get; set; }

    /// <summary>
    /// Indicates whether this action is skipped by conflict policy (e.g. ConflictResolution.Skip).
    /// </summary>
    public bool IsSkipped { get; set; }

    /// <summary>
    /// Informational note regarding policy resolution (e.g., "Auto-renamed due to conflict").
    /// </summary>
    public string? ResolutionNote { get; set; }

    /// <summary>
    /// Conflict resolution strategy associated with this action.
    /// </summary>
    public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Skip;

    /// <summary>
    /// Execution order of this action within the rule.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Fully resolved target file name.
    /// </summary>
    public string? TargetFileName { get; set; }

    /// <summary>
    /// Frozen counter sequence number resolved during planning for this action.
    /// </summary>
    public int? PlannedCounter { get; set; }
}
