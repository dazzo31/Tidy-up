using TidyUp.Data.Entities;

namespace TidyUp.Services.Rollback;

/// <summary>
/// Durable execution journal and rollback engine for reversing completed file operations.
/// </summary>
public interface IRollbackEngine
{
    /// <summary>
    /// Records an executed file operation into the durable SQLite journal.
    /// </summary>
    Task<OperationJournalEntry> RecordOperationAsync(
        OperationJournalEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses a single operation by ID, validating file integrity and destination availability.
    /// </summary>
    Task<RollbackOperationResult> RollbackOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses all operations associated with a batch in reverse chronological order.
    /// </summary>
    Task<RollbackBatchResult> RollbackBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves journal entries for a specific batch.
    /// </summary>
    Task<List<OperationJournalEntry>> GetBatchEntriesAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent journal entries ordered newest first.
    /// </summary>
    Task<List<OperationJournalEntry>> GetRecentEntriesAsync(
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single journal entry by operation ID.
    /// </summary>
    Task<OperationJournalEntry?> GetEntryAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);
}

