using System.IO;
using Microsoft.Data.Sqlite;

namespace TidyUp.Data.Recovery;

public class DbIntegrityChecker : IDbIntegrityChecker
{
    private readonly IDatabaseBackupManager _backupManager;

    public DbIntegrityChecker(IDatabaseBackupManager backupManager)
    {
        _backupManager = backupManager;
    }

    public async Task<DbIntegrityResult> CheckIntegrityAsync(string dbPath)
    {
        var result = new DbIntegrityResult();

        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
        {
            result.IsOk = false;
            result.Messages.Add("Database file does not exist.");
            return result;
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var msg = reader.GetString(0);
                result.Messages.Add(msg);
            }

            result.IsOk = result.Messages.Count == 1 &&
                          string.Equals(result.Messages[0], "ok", StringComparison.OrdinalIgnoreCase);

            await connection.CloseAsync();
            SqliteConnection.ClearAllPools();
        }
        catch (Exception ex)
        {
            result.IsOk = false;
            result.Messages.Add($"Integrity check failed with exception: {ex.Message}");
            SqliteConnection.ClearAllPools();
        }

        return result;
    }

    public async Task<DatabaseStartupResult> SafeInitializeDatabaseAsync(string dbPath, Func<Task> migrationOrCreationAction)
    {
        var startupResult = new DatabaseStartupResult();
        string? preMigrationBackup = null;

        if (File.Exists(dbPath))
        {
            // 1. Check existing integrity
            var integrity = await CheckIntegrityAsync(dbPath);
            if (!integrity.IsOk)
            {
                startupResult.WasCorruptedInitially = true;

                // Attempt to restore from latest healthy backup
                var latestBackup = _backupManager.GetLatestBackup();
                if (!string.IsNullOrWhiteSpace(latestBackup))
                {
                    SqliteConnection.ClearAllPools();
                    var restored = await _backupManager.RestoreBackupAsync(latestBackup, dbPath);
                    if (restored)
                    {
                        startupResult.RestoredFromBackup = true;
                        startupResult.BackupPathUsed = latestBackup;
                    }
                }
            }

            // 2. Create pre-migration backup before touching schema
            SqliteConnection.ClearAllPools();
            preMigrationBackup = await _backupManager.CreateBackupAsync(dbPath, tag: "pre_migration");
        }

        // 3. Run migration / creation action safely
        try
        {
            await migrationOrCreationAction();
            startupResult.Success = true;

            // Ensure daily backup is taken if not already done today
            if (File.Exists(dbPath))
            {
                SqliteConnection.ClearAllPools();
                await _backupManager.EnsureDailyBackupAsync(dbPath);
            }
        }
        catch (Exception ex)
        {
            startupResult.Success = false;
            startupResult.ErrorMessage = $"Database migration/initialization failed: {ex.Message}";

            // Safe recovery: rollback to pre-migration backup if available
            if (!string.IsNullOrWhiteSpace(preMigrationBackup) && File.Exists(preMigrationBackup))
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    var rolledBack = await _backupManager.RestoreBackupAsync(preMigrationBackup, dbPath);
                    if (rolledBack)
                    {
                        startupResult.RestoredFromBackup = true;
                        startupResult.BackupPathUsed = preMigrationBackup;
                    }
                }
                catch (Exception rollbackEx)
                {
                    startupResult.ErrorMessage += $" | Rollback also failed: {rollbackEx.Message}";
                }
            }
        }

        return startupResult;
    }
}

