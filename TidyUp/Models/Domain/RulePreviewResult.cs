namespace TidyUp.Models.Domain;

/// <summary>
/// Represents the preview result for a single file in a dry run.
/// </summary>
public class RulePreviewResult
{
    /// <summary>
    /// Original file path.
    /// </summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the file matches the rule conditions.
    /// </summary>
    public bool Matches { get; set; }

    /// <summary>
    /// Preview of what would happen (list of action descriptions).
    /// </summary>
    public List<ActionPreview> Actions { get; set; } = new();

    /// <summary>
    /// Whether this preview indicates a potential error.
    /// </summary>
    public bool HasWarning { get; set; }

    /// <summary>
    /// Warning or error message if applicable.
    /// </summary>
    public string? WarningMessage { get; set; }
}

/// <summary>
/// Represents a single action preview.
/// </summary>
public class ActionPreview
{
    /// <summary>
    /// Action type (Move, Copy, Rename, etc.).
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Description of what would happen.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Resulting file path after action.
    /// </summary>
    public string? ResultPath { get; set; }

    /// <summary>
    /// Whether this action would cause a conflict.
    /// </summary>
    public bool HasConflict { get; set; }

    /// <summary>
    /// Conflict resolution strategy that would be applied.
    /// </summary>
    public string? ConflictResolution { get; set; }
}
