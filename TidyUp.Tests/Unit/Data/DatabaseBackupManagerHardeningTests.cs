using FluentAssertions;
using Microsoft.Data.Sqlite;
using TidyUp.Data.Recovery;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Data;

public sealed class DatabaseBackupManagerHardeningTests : IDisposable
{
    private readonly IsolatedAppData _appData = new();
    private readonly DatabaseBackupManager _manager;

    public DatabaseBackupManagerHardeningTests()
    {
        _manager = new DatabaseBackupManager(_appData.Paths);
    }

    public void Dispose()
    {
        _appData.Dispose();
    }

    [Fact]
    public async Task CreateBackup_CapturesCommittedRowsStillInTheWal()
    {
        var dbPath = _appData.Paths.DatabasePath;

        // Keep the writer open with checkpointing disabled so committed data lives only in the -wal file.
        using var writer = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        writer.Open();
        Exec(writer, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE t (x INTEGER); INSERT INTO t VALUES (42);");
        new FileInfo(dbPath + "-wal").Length.Should().BeGreaterThan(0, "the scenario needs un-checkpointed WAL data");

        var naiveCopy = Path.Combine(_appData.Paths.AppDataRoot, "naive.db");
        File.Copy(dbPath, naiveCopy);
        var naive = () => Scalar(naiveCopy, "SELECT x FROM t");
        naive.Should().Throw<SqliteException>("a plain copy of the main file misses WAL-resident data");

        var backup = await _manager.CreateBackupAsync(tag: "manual");

        Scalar(backup!, "SELECT x FROM t").Should().Be("42");
    }

    [Fact]
    public async Task CreateBackup_OfACorruptFile_StillPreservesItsBytes()
    {
        Directory.CreateDirectory(_appData.Paths.AppDataRoot);
        await File.WriteAllTextAsync(_appData.Paths.DatabasePath, "NOT A SQLITE DATABASE");

        var backup = await _manager.CreateBackupAsync(tag: "pre_migration");

        (await File.ReadAllTextAsync(backup!)).Should().Be("NOT A SQLITE DATABASE");
    }

    [Fact]
    public async Task Prune_AppliesRetentionPerKind_AndNeverRemovesManualBackups()
    {
        var dir = _appData.Paths.BackupsDirectory;
        Directory.CreateDirectory(dir);
        CreateSimpleDatabase(_appData.Paths.DatabasePath);

        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 20; i++) Touch(Path.Combine(dir, $"tidyup_daily_202601{i + 1:00}.db"), start.AddDays(i));
        for (var i = 0; i < 7; i++) Touch(Path.Combine(dir, $"tidyup_pre_migration_202602{i + 1:00}_120000.db"), start.AddDays(40 + i));
        for (var i = 0; i < 20; i++) Touch(Path.Combine(dir, $"tidyup_manual_202603{i + 1:00}_120000.db"), start.AddDays(80 + i));

        await _manager.CreateBackupAsync(tag: "pre_migration");

        Count(dir, "tidyup_manual_*").Should().Be(20, "manual backups belong to the user");
        Count(dir, "tidyup_daily_*").Should().Be(14);
        Count(dir, "tidyup_pre_migration_*").Should().Be(5);
    }

    [Fact]
    public async Task Restore_RemovesStaleWalAndShmFiles()
    {
        var dbPath = _appData.Paths.DatabasePath;
        CreateSimpleDatabase(dbPath);
        var backup = await _manager.CreateBackupAsync(tag: "manual");

        await File.WriteAllTextAsync(dbPath + "-wal", "stale wal");
        await File.WriteAllTextAsync(dbPath + "-shm", "stale shm");

        (await _manager.RestoreBackupAsync(backup!)).Should().BeTrue();

        File.Exists(dbPath + "-wal").Should().BeFalse();
        File.Exists(dbPath + "-shm").Should().BeFalse();
        Scalar(dbPath, "SELECT x FROM t").Should().Be("1");
    }

    private static int Count(string dir, string pattern) => Directory.GetFiles(dir, pattern + ".db").Length;

    private static void Touch(string path, DateTime creationUtc)
    {
        File.WriteAllText(path, "x");
        File.SetCreationTimeUtc(path, creationUtc);
    }

    private static void CreateSimpleDatabase(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        Exec(connection, "CREATE TABLE t (x INTEGER); INSERT INTO t VALUES (1);");
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string Scalar(string dbPath, string sql)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar())!;
    }
}
