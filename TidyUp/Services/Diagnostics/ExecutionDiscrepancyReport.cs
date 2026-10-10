using TidyUp.Models.Enums;

namespace TidyUp.Services.Diagnostics;

/// <summary>
/// Specific discrepancy classification between preview simulation and physical execution.
/// </summary>
public enum DiscrepancyOutcome
{
    /// <summary>
    /// File operation was completed exactly as simulated in the preview.
    /// </summary>
    ExecutedAsPlanned,

    /// <summary>
    /// File content, size, or timestamp was altered on disk after preview generation.
    /// </summary>
    ModifiedSincePreview,

    /// <summary>
    /// Source file disappeared from disk (deleted or moved externally) before execution started.
    /// </summary>
    FileDisappeared,

    /// <summary>
    /// File was locked by another process (in-progress write, download, or exclusive handle).
    /// </summary>
    SkippedDueToLock,

    /// <summary>
    /// File operation failed due to OS access denial or insufficient permissions.
    /// </summary>
    FailedDueToPermission,

    /// <summary>
    /// File operation failed due to a general I/O or path collision error.
    /// </summary>
    FailedGeneralError,

    /// <summary>
    /// Planned action was not executed (unapproved, excluded, or batch cancelled).
    /// </summary>
    NotExecuted
}

/// <summary>
/// Detailed discrepancy evaluation for an individual planned file action.
/// </summary>
public class ActionDiscrepancyItem
{
    public Guid PlannedActionId { get; init; } = Guid.NewGuid();
    public string SourcePath { get; init; } = string.Empty;
    public string? PlannedTargetPath { get; init; }
    public string? ActualTargetPath { get; init; }
    public ActionType ActionType { get; init; }
    public DiscrepancyOutcome Outcome { get; init; } = DiscrepancyOutcome.ExecutedAsPlanned;
    public bool HasDiscrepancy => Outcome != DiscrepancyOutcome.ExecutedAsPlanned;
    public string Explanation { get; init; } = string.Empty;
    public DateTime? PreviewModifiedTime { get; init; }
    public DateTime? ExecutionModifiedTime { get; init; }
    public long PreviewFileSize { get; init; }
    public long? ExecutionFileSize { get; init; }
}

/// <summary>
/// Post-execution discrepancy audit report comparing planned simulation against physical outcomes.
/// </summary>
public class ExecutionDiscrepancyReport
{
    public Guid BatchId { get; init; } = Guid.NewGuid();
    public DateTime AnalyzedAtUtc { get; init; } = DateTime.UtcNow;
    public int TotalPlanned { get; init; }
    public int ExecutedAsPlannedCount { get; init; }
    public int DiscrepancyCount { get; init; }
    public bool HasAnyDiscrepancies => DiscrepancyCount > 0;
    public List<ActionDiscrepancyItem> Items { get; init; } = new();
    public string SummaryText { get; init; } = string.Empty;
}

