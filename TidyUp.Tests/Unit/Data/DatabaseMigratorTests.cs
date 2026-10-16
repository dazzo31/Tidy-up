using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TidyUp.Data;
using TidyUp.Data.Migrations;
using TidyUp.Services.Rollback;
using TidyUp.Tests.TestInfrastructure;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.Data;

public sealed class DatabaseMigratorTests : IDisposable
{
    private const string PreMigrationPattern = "tidyup_pre_migration_*.db";

    private readonly IsolatedAppData _appData = new();
    private ServiceProvider? _provider;

    private ServiceProvider Provider => _provider ??= (ServiceProvider)ServiceConfiguration.ConfigureServices(_appData.Paths);
    private IDatabaseMigrator Migrator => Provider.GetRequiredService<IDatabaseMigrator>();
    private string DbPath => _appData.Paths.DatabasePath;

    public void Dispose()
    {
        _provider?.Dispose();
        _appData.Dispose();
    }

    [Fact]
    public async Task MigrateAsync_OnMissingDatabase_CreatesFullSchemaWithHistory()
    {
        await Migrator.MigrateAsync();

        Tables(DbPath).Should().Contain(["Rules", "ProcessedFiles", "ActionLogs", "OperationJournal", "RuleRevisions", "__EFMigrationsHistory"]);
        AppliedMigrations(DbPath).Should().HaveCount(2);
    }

    [Fact]
    public async Task MigrateAsync_OnLegacyDatabase_AddsMissingTablesAndPreservesData()
    {
        CreateLegacyDatabase(DbPath, withRule: true);
        Tables(DbPath).Should().NotContain("OperationJournal");

        await Migrator.MigrateAsync();

        Tables(DbPath).Should().Contain(["OperationJournal", "RuleRevisions"]);
        Query(DbPath, "SELECT Name FROM Rules").Should().ContainSingle().Which.Should().Be("Legacy Rule");
        AppliedMigrations(DbPath).Should().HaveCount(2);
    }

    [Fact]
    public async Task MigrateAsync_OnLegacyDatabase_MakesOperationJournalUsable()
    {
        CreateLegacyDatabase(DbPath, withRule: false);

        await Migrator.MigrateAsync();

        var act = () => Query(DbPath, "SELECT COUNT(*) FROM OperationJournal");
        act.Should().NotThrow();
    }

    [Fact]
    public async Task MigrateAsync_WhenAlreadyCurrent_IsIdempotent()
    {
        await Migrator.MigrateAsync();

        (await Migrator.HasPendingMigrationsAsync()).Should().BeFalse();
        var act = async () => await Migrator.MigrateAsync();

        await act.Should().NotThrowAsync();
        AppliedMigrations(DbPath).Should().HaveCount(2);
    }

    [Fact]
    public async Task MigrateAsync_WhenLegacyTableLacksAColumn_RefusesAndLeavesDatabaseUntouched()
    {
        CreateLegacyDatabase(DbPath, withRule: true, omitRulesColumn: "FilesProcessedCount");

        var act = async () => await Migrator.MigrateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*FilesProcessedCount*");
        Tables(DbPath).Should().NotContain("__EFMigrationsHistory");
    }

    [Fact]
    public async Task InitializeDatabase_OnLegacyDatabase_BacksUpOnlyBecauseAMigrationIsPending()
    {
        CreateLegacyDatabase(DbPath, withRule: true);

        var first = await ServiceConfiguration.InitializeDatabaseAsync(Provider);

        first.Success.Should().BeTrue();
        var backup = PreMigrationBackups().Should().ContainSingle().Subject;
        Tables(backup).Should().NotContain("OperationJournal", "the backup must be the pre-migration state");
        Query(backup, "SELECT Name FROM Rules").Should().ContainSingle();

        // A different second ensures a spurious backup would get a distinct file name.
        await Task.Delay(1100);
        var second = await ServiceConfiguration.InitializeDatabaseAsync(Provider);

        second.Success.Should().BeTrue();
        PreMigrationBackups().Should().ContainSingle("an up-to-date database must not be backed up on every launch");
    }

    [Fact]
    public async Task InitializeDatabase_OnFreshInstall_TakesNoPreMigrationBackup()
    {
        var result = await ServiceConfiguration.InitializeDatabaseAsync(Provider);

        result.Success.Should().BeTrue();
        PreMigrationBackups().Should().BeEmpty();
        Tables(DbPath).Should().Contain("OperationJournal");
    }

    [Fact]
    public async Task InitializeDatabase_WhenMigrationFails_RestoresOriginalDatabase()
    {
        CreateLegacyDatabase(DbPath, withRule: true, omitRulesColumn: "FilesProcessedCount");

        var result = await ServiceConfiguration.InitializeDatabaseAsync(Provider);

        result.Success.Should().BeFalse();
        result.RestoredFromBackup.Should().BeTrue();
        result.ErrorMessage.Should().Contain("FilesProcessedCount");
        Query(DbPath, "SELECT Name FROM Rules").Should().ContainSingle().Which.Should().Be("Legacy Rule");
        Tables(DbPath).Should().NotContain("__EFMigrationsHistory");
        File.Exists(DbPath + "-wal").Should().BeFalse("a stale WAL must not survive a restore");
    }

    [Fact]
    public async Task HistoryView_OnLegacyDatabase_ReportsMissingTableBeforeMigration_AndLoadsAfter()
    {
        CreateLegacyDatabase(DbPath, withRule: true);
        var factory = Provider.GetRequiredService<IDbContextFactory<TidyUpDbContext>>();
        var viewModel = new HistoryViewModel(Mock.Of<IRollbackEngine>(), factory);

        await viewModel.LoadPageAsync();
        viewModel.StatusMessage.Should().Contain("no such table: OperationJournal", "this is the symptom reported on upgraded installs");

        await Migrator.MigrateAsync();
        await viewModel.LoadPageAsync();

        viewModel.StatusMessage.Should().NotContain("Error");
        viewModel.StatusMessage.Should().StartWith("Showing 0 of 0");
    }

    private List<string> PreMigrationBackups() =>
        Directory.Exists(_appData.Paths.BackupsDirectory)
            ? Directory.GetFiles(_appData.Paths.BackupsDirectory, PreMigrationPattern).ToList()
            : [];

    private static List<string> Tables(string dbPath) =>
        Query(dbPath, "SELECT name FROM sqlite_master WHERE type = 'table'");

    private static List<string> AppliedMigrations(string dbPath) =>
        Query(dbPath, "SELECT MigrationId FROM __EFMigrationsHistory");

    private static List<string> Query(string dbPath, string sql)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();

        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add(Convert.ToString(reader.GetValue(0))!);
        }

        return rows;
    }

    /// <summary>The exact schema written by the EnsureCreated-era releases (captured from a real v1.0.0 database).</summary>
    private static void CreateLegacyDatabase(string dbPath, bool withRule, string? omitRulesColumn = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        SqliteConnection.ClearAllPools();

        var rulesColumns = new[]
        {
            "\"Id\" TEXT NOT NULL CONSTRAINT \"PK_Rules\" PRIMARY KEY",
            "\"Name\" TEXT NOT NULL",
            "\"Description\" TEXT NULL",
            "\"ConfigJson\" TEXT NOT NULL",
            "\"IsEnabled\" INTEGER NOT NULL",
            "\"ExecutionOrder\" INTEGER NOT NULL",
            "\"StopProcessingAfterMatch\" INTEGER NOT NULL",
            "\"CreatedDate\" TEXT NOT NULL",
            "\"ModifiedDate\" TEXT NOT NULL",
            "\"LastRunDate\" TEXT NULL",
            "\"FilesProcessedCount\" INTEGER NOT NULL"
        }.Where(c => omitRulesColumn == null || !c.StartsWith($"\"{omitRulesColumn}\"")).ToList();

        var insertColumns = rulesColumns.Select(c => c.Split(' ')[0]).ToList();
        var insertValues = insertColumns.Select(c => c switch
        {
            "\"Id\"" => "'121D300C-CE03-451F-A86D-03F6539CCC46'",
            "\"Name\"" => "'Legacy Rule'",
            "\"ConfigJson\"" => "'{}'",
            "\"CreatedDate\"" or "\"ModifiedDate\"" => "'2026-10-10 23:21:47'",
            "\"Description\"" or "\"LastRunDate\"" => "NULL",
            _ => "0"
        });

        var sql = $"""
            PRAGMA journal_mode=WAL;
            CREATE TABLE "Rules" ({string.Join(", ", rulesColumns)});
            CREATE TABLE "ProcessedFiles" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ProcessedFiles" PRIMARY KEY,
                "RuleId" TEXT NOT NULL, "FilePath" TEXT NOT NULL, "FileHash" TEXT NOT NULL,
                "LastProcessed" TEXT NOT NULL, "FileSize" INTEGER NOT NULL, "IsPending" INTEGER NOT NULL);
            CREATE TABLE "ActionLogs" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ActionLogs" PRIMARY KEY,
                "Timestamp" TEXT NOT NULL, "RuleId" TEXT NOT NULL, "RuleName" TEXT NOT NULL,
                "FilePath" TEXT NOT NULL, "ActionPerformed" TEXT NOT NULL, "ResultPath" TEXT NULL,
                "Status" TEXT NOT NULL, "ErrorMessage" TEXT NULL);
            CREATE INDEX "IX_ActionLogs_RuleId" ON "ActionLogs" ("RuleId");
            CREATE INDEX "IX_ActionLogs_Timestamp" ON "ActionLogs" ("Timestamp");
            CREATE UNIQUE INDEX "IX_ProcessedFiles_FilePath_RuleId" ON "ProcessedFiles" ("FilePath", "RuleId");
            CREATE INDEX "IX_Rules_ExecutionOrder" ON "Rules" ("ExecutionOrder");
            """;

        using (var connection = new SqliteConnection(
                   new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();

            if (withRule)
            {
                command.CommandText =
                    $"INSERT INTO \"Rules\" ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", insertValues)});";
                command.ExecuteNonQuery();
            }
        }

        SqliteConnection.ClearAllPools();
    }
}
