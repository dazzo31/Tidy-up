using System.IO;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data;
using TidyUp.Data.Entities;
using TidyUp.Services.FileSystem;

namespace TidyUp.Services.Rollback;

/// <summary>
/// Durable SQLite execution journal and rollback engine for reversing completed file operations.
/// </summary>
public class RollbackEngine : IRollbackEngine
{
    private readonly IDbContextFactory<TidyUpDbContext>? _contextFactory;
    private readonly TidyUpDbContext? _directContext;
    private readonly ISafeFileSystem _safeFileSystem;

    public RollbackEngine(
        IDbContextFactory<TidyUpDbContext>? contextFactory = null,
        TidyUpDbContext? directContext = null,
        ISafeFileSystem? safeFileSystem = null)
    {
        if (contextFactory is null && directContext is null)
            throw new ArgumentException("Either contextFactory or directContext must be provided.");

        _contextFactory = contextFactory;
        _directContext = directContext;
        _safeFileSystem = safeFileSystem ?? new WindowsShellFileOperations();
    }

    private async Task<(TidyUpDbContext Context, bool MustDispose)> GetContextAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return (ctx, true);
        }

        return (_directContext!, false);
    }

    /// <inheritdoc />
    public async Task<OperationJournalEntry> RecordOperationAsync(
        OperationJournalEntry entry,
        CancellationToken cancellationToken = default)
    {
        var (context, mustDispose) = await GetContextAsync(cancellationToken);
        try
        {
            context.OperationJournal.Add(entry);
            await context.SaveChangesAsync(cancellationToken);
            return entry;
        }
        finally
        {
            if (mustDispose)
                await context.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public async Task<RollbackOperationResult> RollbackOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var (context, mustDispose) = await GetContextAsync(cancellationToken);
        try
        {
            var entry = await context.OperationJournal
                .FirstOrDefaultAsync(e => e.OperationId == operationId, cancellationToken);

            if (entry is null)
            {
                return new RollbackOperationResult
                {
                    OperationId = operationId,
                    Success = false,
                    Message = $"Operation '{operationId}' was not found in the journal.",
                    ConflictReason = RollbackConflictReason.EntryNotFound
                };
            }

            if (string.Equals(entry.Status, "RolledBack", StringComparison.OrdinalIgnoreCase))
            {
                return new RollbackOperationResult
                {
                    OperationId = operationId,
                    BatchId = entry.BatchId,
                    ActionType = entry.ActionType,
                    OriginalPath = entry.OriginalPath,
                    TargetPath = entry.TargetPath,
                    Success = false,
                    Message = "Operation has already been rolled back.",
                    ConflictReason = RollbackConflictReason.AlreadyRolledBack
                };
            }

            return entry.ActionType switch
            {
                "Move" or "Rename" or "ChangeExtension" =>
                    await RollbackMoveOrRenameAsync(context, entry, cancellationToken),

                "Copy" =>
                    await RollbackCopyAsync(context, entry, cancellationToken),

                "Delete" =>
                    await RollbackDeleteAsync(context, entry, cancellationToken),

                _ => new RollbackOperationResult
                {
                    OperationId = entry.OperationId,
                    BatchId = entry.BatchId,
                    ActionType = entry.ActionType,
                    OriginalPath = entry.OriginalPath,
                    TargetPath = entry.TargetPath,
                    Success = false,
                    Message = $"Action type '{entry.ActionType}' is not supported for automatic rollback.",
                    ConflictReason = RollbackConflictReason.UnsupportedActionType
                }
            };
        }
        finally
        {
            if (mustDispose)
                await context.DisposeAsync();
        }
    }

    private static async Task<RollbackOperationResult> RollbackMoveOrRenameAsync(
        TidyUpDbContext context,
        OperationJournalEntry entry,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.TargetPath) || !File.Exists(entry.TargetPath))
        {
            return new RollbackOperationResult
            {
                OperationId = entry.OperationId,
                BatchId = entry.BatchId,
                ActionType = entry.ActionType,
                OriginalPath = entry.OriginalPath,
                TargetPath = entry.TargetPath,
                Success = false,
                Message = $"Target file '{entry.TargetPath}' no longer exists on disk.",
                ConflictReason = RollbackConflictReason.TargetMissing
            };
        }

        // Anti-clobbering: verify target file hash matches post-action hash
        if (!string.IsNullOrEmpty(entry.PostActionHash))
        {
            var currentHash = FileChecksumHelper.ComputeSha256(entry.TargetPath);
            if (!string.Equals(currentHash, entry.PostActionHash, StringComparison.OrdinalIgnoreCase))
            {
                return new RollbackOperationResult
                {
                    OperationId = entry.OperationId,
                    BatchId = entry.BatchId,
                    ActionType = entry.ActionType,
                    OriginalPath = entry.OriginalPath,
                    TargetPath = entry.TargetPath,
                    Success = false,
                    Message = $"Target file '{entry.TargetPath}' was modified by an external process after operation. Rollback aborted to protect user changes.",
                    ConflictReason = RollbackConflictReason.TargetModifiedByExternalProcess
                };
            }
        }

        // Anti-clobbering: verify original path is not occupied
        if (File.Exists(entry.OriginalPath))
        {
            return new RollbackOperationResult
            {
                OperationId = entry.OperationId,
                BatchId = entry.BatchId,
                ActionType = entry.ActionType,
                OriginalPath = entry.OriginalPath,
                TargetPath = entry.TargetPath,
                Success = false,
                Message = $"Original path '{entry.OriginalPath}' is occupied by another file. Rollback aborted to prevent clobbering.",
                ConflictReason = RollbackConflictReason.OriginalDestinationOccupied
            };
        }

        try
        {
            var originalDir = Path.GetDirectoryName(entry.OriginalPath);
            if (!string.IsNullOrEmpty(originalDir) && !Directory.Exists(originalDir))
            {
                Directory.CreateDirectory(originalDir);
            }

            File.Move(entry.TargetPath, entry.OriginalPath);

            entry.Status = "RolledBack";
            entry.RolledBackAt = DateTime.UtcNow;
            entry.Details = string.IsNullOrWhiteSpace(entry.Details)
                ? "Rolled back successfully"
                : $"{entry.Details}; Rolled back successfully";

            await context.SaveChangesAsync(cancellationToken);

            return new RollbackOperationResult
            {
                OperationId = entry.OperationId,
                BatchId = entry.BatchId,
                ActionType = entry.ActionType,
                OriginalPath = entry.OriginalPath,
                TargetPath = entry.TargetPath,
                Success = true,
                Message = $"File successfully restored to '{entry.OriginalPath}'."
            };
        }
        catch (Exception ex)
        {
            return new RollbackOperationResult
            {
                OperationId = entry.OperationId,
                BatchId = entry.BatchId,
                ActionType = entry.ActionType,
                OriginalPath = entry.OriginalPath,
                TargetPath = entry.TargetPath,
                Success = false,
                Message = $"Filesystem error during rollback: {ex.Message}",
                ConflictReason = RollbackConflictReason.FileSystemError
            };
        }
    }

    private async Task<RollbackOperationResult> RollbackCopyAsync(
        TidyUpDbContext context,
        OperationJournalEntry entry,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.TargetPath) || !File.Exists(entry.TargetPath))
        {
            entry.Status = "RolledBack";
            entry.RolledBackAt = DateTime.UtcNow;
            entry.Details = $"{entry.Details}; Target copy already absent".TrimStart(';', ' ');
            await context.SaveChangesAsync(cancellationToken);

            return new RollbackOperationResult
            {
                OperationId = entry.OperationId,
                BatchId = entry.BatchId,
                ActionType = entry.ActionType,
                OriginalPath = entry.OriginalPath,
                TargetPath = entry.TargetPath,
                Success = true,
                Message = "Target copy was already removed."
            };
        }

        // Verify copied target hasn't been modified
        if (!string.IsNullOrEmpty(entry.PostActionHash))
        {
            var currentHash = FileChecksumHelper.ComputeSha256(entry.TargetPath);
            if (!string.Equals(currentHash, entry.PostActionHash, StringComparison.OrdinalIgnoreCase))
            {
                return new RollbackOperationResult
                {
                    OperationId = entry.OperationId,
                    BatchId = entry.BatchId,
                    ActionType = entry.ActionType,
                    OriginalPath = entry.OriginalPath,
                    TargetPath = entry.TargetPath,
                    Success = false,
                    Message = $"Target copy '{entry.TargetPath}' was modified by user. Rollback aborted to prevent data loss.",
                    ConflictReason = RollbackConflictReason.TargetModifiedByExternalProcess
                };
            }
        }

        try
        {
            await _safeFileSystem.DeleteFileSafelyAsync(
                entry.TargetPath,
                useRecycleBin: true,
                allowPermanentFallback: true);

            entry.Status = "RolledBack";
            entry.RolledBackAt = DateTime.UtcNow;
            entry.Details = $"{entry.Details}; Copied file removed".TrimStart(';', ' ');
            await context.SaveChangesAsync(cancellationToken);

            return new RollbackOperationResult
            {
                OperationId = entry.OperationId,
                BatchId = entry.BatchId,
                ActionType = entry.ActionType,
                OriginalPath = entry.OriginalPath,
                TargetPath = entry.TargetPath,
                Success = true,
                Message = $"Copied file '{entry.TargetPath}' was removed."
            };
        }
        catch (Exception ex)
        {
            return new RollbackOperationResult
            {
                OperationId = entry.OperationId,
                BatchId = entry.BatchId,
                ActionType = entry.ActionType,
                OriginalPath = entry.OriginalPath,
                TargetPath = entry.TargetPath,
                Success = false,
                Message = $"Filesystem error during copy removal: {ex.Message}",
                ConflictReason = RollbackConflictReason.FileSystemError
            };
        }
    }

    private static async Task<RollbackOperationResult> RollbackDeleteAsync(
        TidyUpDbContext context,
        OperationJournalEntry entry,
        CancellationToken cancellationToken)
    {
        if (File.Exists(entry.OriginalPath))
        {
            return new RollbackOperationResult
            {
                OperationId = entry.OperationId,
                BatchId = entry.BatchId,
                ActionType = entry.ActionType,
                OriginalPath = entry.OriginalPath,
                TargetPath = entry.TargetPath,
                Success = false,
                Message = $"Original destination '{entry.OriginalPath}' is occupied by another file.",
                ConflictReason = RollbackConflictReason.OriginalDestinationOccupied
            };
        }

        var fileName = Path.GetFileName(entry.OriginalPath);
        var instructions = $"The file '{fileName}' was sent to the Windows Recycle Bin on {entry.Timestamp:yyyy-MM-dd HH:mm:ss} UTC. To restore: Open the Windows Recycle Bin, right-click '{fileName}', and select 'Restore' to return it to '{entry.OriginalPath}'.";

        entry.Details = $"{entry.Details}; Recycle Bin restoration instructions generated".TrimStart(';', ' ');
        await context.SaveChangesAsync(cancellationToken);

        return new RollbackOperationResult
        {
            OperationId = entry.OperationId,
            BatchId = entry.BatchId,
            ActionType = entry.ActionType,
            OriginalPath = entry.OriginalPath,
            TargetPath = entry.TargetPath,
            Success = true,
            Message = "Deletion documented with Windows Recycle Bin restoration instructions.",
            ConflictReason = RollbackConflictReason.RecycleBinRestorationGuidance,
            RestorationInstructions = instructions
        };
    }

    /// <inheritdoc />
    public async Task<RollbackBatchResult> RollbackBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var entries = await GetBatchEntriesAsync(batchId, cancellationToken);
        var results = new List<RollbackOperationResult>();

        // Reversal occurs in reverse order of execution (newest first)
        foreach (var entry in entries.OrderByDescending(e => e.Timestamp))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var opResult = await RollbackOperationAsync(entry.OperationId, cancellationToken);
            results.Add(opResult);
        }

        return new RollbackBatchResult
        {
            BatchId = batchId,
            TotalOperations = results.Count,
            RolledBackCount = results.Count(r => r.Success),
            FailedCount = results.Count(r => !r.Success),
            OverallSuccess = results.All(r => r.Success),
            OperationResults = results
        };
    }

    /// <inheritdoc />
    public async Task<List<OperationJournalEntry>> GetBatchEntriesAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var (context, mustDispose) = await GetContextAsync(cancellationToken);
        try
        {
            return await context.OperationJournal
                .Where(e => e.BatchId == batchId)
                .OrderBy(e => e.Timestamp)
                .ToListAsync(cancellationToken);
        }
        finally
        {
            if (mustDispose)
                await context.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public async Task<List<OperationJournalEntry>> GetRecentEntriesAsync(
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var (context, mustDispose) = await GetContextAsync(cancellationToken);
        try
        {
            return await context.OperationJournal
                .OrderByDescending(e => e.Timestamp)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }
        finally
        {
            if (mustDispose)
                await context.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public async Task<OperationJournalEntry?> GetEntryAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var (context, mustDispose) = await GetContextAsync(cancellationToken);
        try
        {
            return await context.OperationJournal
                .FirstOrDefaultAsync(e => e.OperationId == operationId, cancellationToken);
        }
        finally
        {
            if (mustDispose)
                await context.DisposeAsync();
        }
    }
}

