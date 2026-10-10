using System.IO;
using Microsoft.Data.Sqlite;
using TidyUp.Data.Recovery;
using Xunit;

namespace TidyUp.Tests.Unit.Data;

public class DatabaseRecoveryTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly string _backupsDir;
    private readonly DatabaseBackupManager _backupManager;
    private readonly DbIntegrityChecker _integrityChecker;

    public DatabaseRecoveryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TidyUp_DbRecoveryTests_" + Guid.NewGuid().ToString("N"));
        _dbPath = Path.Combine(_testDir, "test.db");
        _backupsDir = Path.Combine(_testDir, "Backups");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_backupsDir);

        _backupManager = new DatabaseBackupManager(_dbPath, _backupsDir);
        _integrityChecker = new DbIntegrityChecker(_backupManager);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // Ignore cleanup
            }
        }
    }

    private void CreateValidSqliteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE TestTable (Id INTEGER PRIMARY KEY, Val TEXT); INSERT INTO TestTable VALUES (1, 'Initial');";
        command.ExecuteNonQuery();
        connection.Close();
        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task CheckIntegrityAsync_ValidDatabase_ReturnsIsOkTrue()
    {
        // Arrange
        CreateValidSqliteDatabase(_dbPath);

        // Act
        var result = await _integrityChecker.CheckIntegrityAsync(_dbPath);

        // Assert
        Assert.True(result.IsOk);
        Assert.Single(result.Messages);
        Assert.Equal("ok", result.Messages[0]);
    }

    [Fact]
    public async Task CheckIntegrityAsync_CorruptFile_ReturnsIsOkFalse()
    {
        // Arrange: Corrupt file (random binary noise not valid SQLite header)
        var garbage = new byte[1024];
        Random.Shared.NextBytes(garbage);
        await File.WriteAllBytesAsync(_dbPath, garbage);

        // Act
        var result = await _integrityChecker.CheckIntegrityAsync(_dbPath);

        // Assert
        Assert.False(result.IsOk);
        Assert.NotEmpty(result.Messages);
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesTimestampedCopy_AndAllowsRestoration()
    {
        // Arrange
        CreateValidSqliteDatabase(_dbPath);

        // Act: Create backup
        var backupFile = await _backupManager.CreateBackupAsync(_dbPath, tag: "test_run");

        // Assert
        Assert.NotNull(backupFile);
        Assert.True(File.Exists(backupFile));
        Assert.Contains("test_run", backupFile);

        // Corrupt original DB
        SqliteConnection.ClearAllPools();
        await File.WriteAllTextAsync(_dbPath, "COMPLETELY CORRUPTED CONTENT");
        var corruptIntegrity = await _integrityChecker.CheckIntegrityAsync(_dbPath);
        Assert.False(corruptIntegrity.IsOk);

        // Act: Restore backup
        var restored = await _backupManager.RestoreBackupAsync(backupFile, _dbPath);

        // Assert restoration succeeded and DB is valid again
        Assert.True(restored);
        var restoredIntegrity = await _integrityChecker.CheckIntegrityAsync(_dbPath);
        Assert.True(restoredIntegrity.IsOk);
    }

    [Fact]
    public async Task EnsureDailyBackupAsync_CreatesOnlyOneBackupPerDay()
    {
        // Arrange
        CreateValidSqliteDatabase(_dbPath);

        // Act: Call twice
        var firstBackup = await _backupManager.EnsureDailyBackupAsync(_dbPath);
        var secondBackup = await _backupManager.EnsureDailyBackupAsync(_dbPath);

        // Assert
        Assert.NotNull(firstBackup);
        Assert.NotNull(secondBackup);
        Assert.Equal(firstBackup, secondBackup);

        var backups = _backupManager.GetAvailableBackups();
        Assert.Single(backups);
    }

    [Fact]
    public async Task SafeInitializeDatabaseAsync_WhenMigrationFails_RollsBackToPreMigrationBackup()
    {
        // Arrange: Start with valid DB containing table
        CreateValidSqliteDatabase(_dbPath);

        // Act: Safe initialize where migration action throws exception
        var result = await _integrityChecker.SafeInitializeDatabaseAsync(_dbPath, () =>
        {
            // Simulate migration failure
            throw new InvalidOperationException("Simulated schema migration failure!");
        });

        // Assert
        Assert.False(result.Success);
        Assert.True(result.RestoredFromBackup, "Should automatically restore pre-migration backup");
        Assert.NotNull(result.BackupPathUsed);
        Assert.Contains("Simulated schema migration failure", result.ErrorMessage);

        // Verify the database is still in healthy pre-migration state
        var postRollbackIntegrity = await _integrityChecker.CheckIntegrityAsync(_dbPath);
        Assert.True(postRollbackIntegrity.IsOk);
    }

    [Fact]
    public async Task SafeInitializeDatabaseAsync_WhenCorruptedInitially_RestoresFromLatestBackup()
    {
        // Arrange: Valid backup exists, but primary DB is corrupted
        var backupPath = Path.Combine(_backupsDir, "tidyup_manual_20261010_120000.db");
        CreateValidSqliteDatabase(backupPath);

        await File.WriteAllTextAsync(_dbPath, "CORRUPTED DATABASE FILE");

        // Act
        var result = await _integrityChecker.SafeInitializeDatabaseAsync(_dbPath, () => Task.CompletedTask);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.WasCorruptedInitially);
        Assert.True(result.RestoredFromBackup);
        Assert.Equal(backupPath, result.BackupPathUsed);

        var integrity = await _integrityChecker.CheckIntegrityAsync(_dbPath);
        Assert.True(integrity.IsOk);
    }
}

