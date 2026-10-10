using TidyUp.Models.Domain;

namespace TidyUp.Services.Processing;

/// <summary>
/// Status of a batch operation.
/// </summary>
public enum BatchExecutionStatus
{
    NotStarted,
    Running,
    Completed,
    PartiallyCompleted,
    Failed
}

/// <summary>
/// Result of executing an individual planned file action in a batch.
/// </summary>
public class BatchItemExecutionResult
{
    public PlannedFileAction Action { get; set; } = null!;
    public ActionResult? Result { get; set; }
    public bool Success => Result?.Success == true;
    public bool Skipped { get; set; }
    public string? ErrorMessage => Result?.ErrorMessage;
}

/// <summary>
/// Progress reporting payload for batch operations.
/// </summary>
public class BatchProgressReport
{
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public double Percentage => TotalCount > 0 ? ((double)CompletedCount / TotalCount) * 100.0 : 0.0;
    public PlannedFileAction? CurrentAction { get; set; }
    public ActionResult? LastActionResult { get; set; }
}

/// <summary>
/// Aggregate result of an entire coordinated batch run.
/// </summary>
public class BatchExecutionResult
{
    public Guid BatchId { get; set; }
    public BatchExecutionStatus Status { get; set; }
    public int TotalItems { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int CancelledOrSkippedCount { get; set; }
    public List<BatchItemExecutionResult> ItemResults { get; set; } = new();
    public bool WasCancelled => Status == BatchExecutionStatus.PartiallyCompleted;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Coordinates batch file operations with cooperative cancellation, atomic file completion, and progress tracking.
/// </summary>
public interface IBatchProcessingCoordinator
{
    /// <summary>
    /// Indicates whether a batch is currently executing.
    /// </summary>
    bool IsExecuting { get; }

    /// <summary>
    /// Executes a collection of planned file actions as an atomic, cooperatively cancellable batch.
    /// </summary>
    Task<BatchExecutionResult> ExecuteBatchAsync(
        IReadOnlyList<PlannedFileAction> actions,
        Guid batchId,
        IProgress<BatchProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}

