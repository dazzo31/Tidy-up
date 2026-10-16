using System.IO;
using Microsoft.Data.Sqlite;

namespace TidyUp.Configuration;

/// <summary>
/// Single source of truth for where TidyUp stores its database, settings, and backups.
/// </summary>
public sealed record TidyUpPaths
{
    public const string EnvironmentVariableName = "TIDYUP_APPDATA";

    public TidyUpPaths(string appDataRoot)
    {
        if (string.IsNullOrWhiteSpace(appDataRoot))
            throw new ArgumentException("App data root must not be empty.", nameof(appDataRoot));

        AppDataRoot = Path.GetFullPath(appDataRoot);
    }

    public string AppDataRoot { get; }
    public string DatabasePath => Path.Combine(AppDataRoot, "tidyup.db");
    public string SettingsPath => Path.Combine(AppDataRoot, "settings.json");
    public string BackupsDirectory => Path.Combine(AppDataRoot, "Backups");

    public string ConnectionString =>
        new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();

    /// <summary>The real per-user location (%APPDATA%\TidyUp). Tests must never call this.</summary>
    public static TidyUpPaths Default() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TidyUp"));

    public static TidyUpPaths FromEnvironmentOrDefault()
    {
        var overridePath = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        return string.IsNullOrWhiteSpace(overridePath) ? Default() : new TidyUpPaths(overridePath);
    }
}
