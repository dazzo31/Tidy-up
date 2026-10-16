using System.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TidyUp.Data.Migrations;

public sealed class DatabaseMigrator(IDbContextFactory<TidyUpDbContext> contextFactory) : IDatabaseMigrator
{
    private const string BaselineMigrationName = "InitialCreate";

    // The EnsureCreated-era schema. Baselining is refused unless every column is present.
    private static readonly IReadOnlyDictionary<string, string[]> LegacyColumns = new Dictionary<string, string[]>
    {
        ["Rules"] =
        [
            "Id", "Name", "Description", "ConfigJson", "IsEnabled", "ExecutionOrder",
            "StopProcessingAfterMatch", "CreatedDate", "ModifiedDate", "LastRunDate", "FilesProcessedCount"
        ],
        ["ProcessedFiles"] = ["Id", "RuleId", "FilePath", "FileHash", "LastProcessed", "FileSize", "IsPending"],
        ["ActionLogs"] =
        [
            "Id", "Timestamp", "RuleId", "RuleName", "FilePath", "ActionPerformed", "ResultPath", "Status", "ErrorMessage"
        ]
    };

    public async Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (!(await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            return;
        }

        var hasHistory = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Any();
        if (!hasHistory && await TableExistsAsync(context, "Rules", cancellationToken))
        {
            await BaselineLegacySchemaAsync(context, cancellationToken);
        }

        await context.Database.MigrateAsync(cancellationToken);
        await VerifyIntegrityAsync(context, cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(DbContext context, string table, CancellationToken cancellationToken)
    {
        var count = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {table}")
            .SingleAsync(cancellationToken);
        return count > 0;
    }

    private static async Task BaselineLegacySchemaAsync(DbContext context, CancellationToken cancellationToken)
    {
        foreach (var (table, expected) in LegacyColumns)
        {
            var actual = await context.Database
                .SqlQuery<string>($"SELECT name AS Value FROM pragma_table_info({table})")
                .ToListAsync(cancellationToken);

            if (actual.Count == 0)
            {
                throw new InvalidOperationException($"Existing database is missing the '{table}' table; it cannot be upgraded safely.");
            }

            var missing = expected.Except(actual, StringComparer.OrdinalIgnoreCase).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Existing database table '{table}' is missing column(s) {string.Join(", ", missing)}; it cannot be upgraded safely.");
            }
        }

        var baselineId = context.Database.GetMigrations()
            .Single(m => m.EndsWith($"_{BaselineMigrationName}", StringComparison.Ordinal));

        var history = context.GetService<IHistoryRepository>();
        await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            history.GetInsertScript(new HistoryRow(baselineId, EfProductVersion())), cancellationToken);
    }

    private static async Task VerifyIntegrityAsync(DbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));

            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Database failed its integrity check after migration: {result}");
            }
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string EfProductVersion() =>
        typeof(DbContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] ?? "9.0.0";
}
