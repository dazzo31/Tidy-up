using TidyUp.Models.Enums;

namespace TidyUp.Models.Domain;

/// <summary>
/// Represents an in-memory planned file action produced during rule simulation.
/// </summary>
public class PlannedFileAction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The source file path.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

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
    /// Size of the source file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Last modified date of the source file.
    /// </summary>
    public DateTime SourceModifiedDate { get; set; }

    /// <summary>
    /// Whether this action encounters a conflict (e.g., target already exists).
    /// </summary>
    public bool HasConflict { get; set; }

    /// <summary>
    /// Description of the conflict if one exists.
    /// </summary>
    public string? ConflictDescription { get; set; }

    /// <summary>
    /// Conflict resolution strategy associated with this action.
    /// </summary>
    public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Skip;

    /// <summary>
    /// Execution order of this action within the rule.
    /// </summary>
    public int Order { get; set; }
}

