namespace TidyUp.Data.Migrations;

public interface IDatabaseMigrator
{
    /// <summary>True when the schema is behind the latest migration. Only call on an existing database file.</summary>
    Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings the database to the latest schema. Databases created by the pre-migration releases
    /// (EnsureCreated, no history table) are baselined first. Throws on any failure so the caller can restore its backup.
    /// </summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
