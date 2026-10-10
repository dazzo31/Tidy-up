namespace TidyUp.Services.Rollback;

/// <summary>
/// Specific conflict or failure reasons that prevent an automatic operation rollback.
/// </summary>
public enum RollbackConflictReason
{
    None = 0,
    EntryNotFound,
    AlreadyRolledBack,
    TargetMissing,
    TargetModifiedByExternalProcess,
    OriginalDestinationOccupied,
    RecycleBinRestorationGuidance,
    UnsupportedActionType,
    FileSystemError
}

/// <summary>
/// Outcome of rolling back a single journaled operation.
/// </summary>
public class RollbackOperationResult
{
    public Guid OperationId { get; init; }
    public Guid BatchId { get; init; }
    public bool Success { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string OriginalPath { get; init; } = string.Empty;
    public string? TargetPath { get; init; }
    public string Message { get; init; } = string.Empty;
    public RollbackConflictReason ConflictReason { get; init; } = RollbackConflictReason.None;
    public string? RestorationInstructions { get; init; }
}

/// <summary>
/// Outcome of rolling back an entire batch of operations.
/// </summary>
public class RollbackBatchResult
{
    public Guid BatchId { get; init; }
    public bool OverallSuccess { get; init; }
    public int TotalOperations { get; init; }
    public int RolledBackCount { get; init; }
    public int FailedCount { get; init; }
    public List<RollbackOperationResult> OperationResults { get; init; } = new();
}

