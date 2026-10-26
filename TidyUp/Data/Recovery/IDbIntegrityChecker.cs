using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TidyUp.Data.Recovery;

public class DbIntegrityResult
{
    public bool IsOk { get; set; }
    public List<string> Messages { get; set; } = [];
}

public class DatabaseStartupResult
{
    public bool Success { get; set; }
    public bool WasCorruptedInitially { get; set; }
    public bool RestoredFromBackup { get; set; }
    public string? BackupPathUsed { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DatabaseMaintenanceResult
{
    public bool Success { get; set; }
    public long PreSizeBytes { get; set; }
    public long PostSizeBytes { get; set; }
    public long ReclaimedBytes => Math.Max(0, PreSizeBytes - PostSizeBytes);
    public long PreWalSizeBytes { get; set; }
    public long PostWalSizeBytes { get; set; }
    public long PreShmSizeBytes { get; set; }
    public long PostShmSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IDbIntegrityChecker
{
    Task<DbIntegrityResult> CheckIntegrityAsync(string dbPath);

    /// <param name="needsPreActionBackup">
    /// When supplied, the pre-migration backup is taken only if this returns true. When null a backup is always taken.
    /// </param>
    Task<DatabaseStartupResult> SafeInitializeDatabaseAsync(
        string dbPath,
        Func<Task> migrationOrCreationAction,
        Func<Task<bool>>? needsPreActionBackup = null);

    /// <summary>
    /// Executes PRAGMA wal_checkpoint(TRUNCATE) followed by VACUUM on the SQLite database,
    /// measuring space before and after the operation.
    /// </summary>
    Task<DatabaseMaintenanceResult> VacuumDatabaseAsync(string dbPath);
}
