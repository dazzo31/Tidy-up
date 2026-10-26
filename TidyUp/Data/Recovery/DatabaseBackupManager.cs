using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TidyUp.Configuration;

namespace TidyUp.Data.Recovery;

public partial class DatabaseBackupManager : IDatabaseBackupManager
{
    // Per-kind retention. Manual and untagged backups are the user's and are never pruned automatically.
    private static readonly IReadOnlyDictionary<string, int> RetentionByTag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["daily"] = 14,
        ["pre_migration"] = 5
    };

    private readonly string _defaultDatabasePath;
    private readonly string _backupsDirectory;

    public string DefaultDatabasePath => _defaultDatabasePath;
    public string BackupsDirectory => _backupsDirectory;
    private readonly TidyUp.Models.AppSettings? _appSettings;

    public DatabaseBackupManager(string databasePath, string backupsDirectory, TidyUp.Models.AppSettings? appSettings = null)
    {
        _defaultDatabasePath = databasePath;
        _backupsDirectory = backupsDirectory;
        _appSettings = appSettings;
    }

    public DatabaseBackupManager(TidyUpPaths paths, TidyUp.Models.AppSettings? appSettings = null) : this(paths.DatabasePath, paths.BackupsDirectory, appSettings)
    {
    }

    public async Task<string?> CreateBackupAsync(string? sourceDbPath = null, string? destinationPath = null, string? tag = null)
    {
        var source = sourceDbPath ?? _defaultDatabasePath;
        if (!File.Exists(source))
            return null;

        var effectiveBackupsDir = !string.IsNullOrWhiteSpace(_appSettings?.DatabaseBackupPath)
            ? _appSettings.DatabaseBackupPath
            : _backupsDirectory;

        Directory.CreateDirectory(effectiveBackupsDir);

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            var cleanTag = string.IsNullOrWhiteSpace(tag) ? "backup" : tag.Trim();
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            destinationPath = Path.Combine(effectiveBackupsDir, $"tidyup_{cleanTag}_{timestamp}.db");
        }
        else
        {
            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destDir))
                Directory.CreateDirectory(destDir);
        }

        if (!TryOnlineBackup(source, destinationPath))
        {
            // Not a readable SQLite database (e.g. corrupt): keep the raw bytes as evidence.
            await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await using var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destStream);
        }

        PruneOldBackups();
        return destinationPath;
    }

    // The online backup API captures committed data that is still in the WAL, which a plain file copy would miss.
    private static bool TryOnlineBackup(string source, string destination)
    {
        try
        {
            if (File.Exists(destination))
                File.Delete(destination);

            using (var sourceConnection = new SqliteConnection(
                       new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            using (var destinationConnection = new SqliteConnection(
                       new SqliteConnectionStringBuilder { DataSource = destination, Pooling = false }.ToString()))
            {
                sourceConnection.Open();
                sourceConnection.BackupDatabase(destinationConnection);
            }

            SqliteConnection.ClearAllPools();
            return true;
        }
        catch (SqliteException)
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(destination))
                File.Delete(destination);
            return false;
        }
    }

    public async Task<string?> EnsureDailyBackupAsync(string? sourceDbPath = null)
    {
        var source = sourceDbPath ?? _defaultDatabasePath;
        if (!File.Exists(source))
            return null;

        var effectiveBackupsDir = !string.IsNullOrWhiteSpace(_appSettings?.DatabaseBackupPath)
            ? _appSettings.DatabaseBackupPath
            : _backupsDirectory;

        Directory.CreateDirectory(effectiveBackupsDir);

        var todayTag = DateTime.UtcNow.ToString("yyyyMMdd");
        var expectedDailyFileName = $"tidyup_daily_{todayTag}.db";
        var dailyPath = Path.Combine(effectiveBackupsDir, expectedDailyFileName);

        if (File.Exists(dailyPath))
        {
            return dailyPath; // Daily backup for today already taken
        }

        return await CreateBackupAsync(source, dailyPath, "daily");
    }

    public async Task<bool> RestoreBackupAsync(string backupFilePath, string? targetDbPath = null)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            return false;

        var target = targetDbPath ?? _defaultDatabasePath;
        var targetDir = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(targetDir))
            Directory.CreateDirectory(targetDir);

        // A leftover WAL/SHM from the replaced database would be replayed onto the restored file.
        SqliteConnection.ClearAllPools();
        foreach (var sidecar in new[] { target + "-wal", target + "-shm" })
        {
            if (File.Exists(sidecar))
                File.Delete(sidecar);
        }

        // Copy backup to target with overwrite
        await using (var sourceStream = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        await using (var destStream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await sourceStream.CopyToAsync(destStream);
        }

        return true;
    }

    public List<string> GetAvailableBackups()
    {
        if (!Directory.Exists(_backupsDirectory))
            return [];

        return Directory.GetFiles(_backupsDirectory, "tidyup_*.db")
            .OrderByDescending(f => File.GetCreationTimeUtc(f))
            .ToList();
    }

    public string? GetLatestBackup()
    {
        return GetAvailableBackups().FirstOrDefault();
    }

    private void PruneOldBackups()
    {
        try
        {
            var byTag = GetAvailableBackups()
                .Select(path => (Path: path, Tag: ParseTag(path)))
                .Where(b => b.Tag != null && RetentionByTag.ContainsKey(b.Tag))
                .GroupBy(b => b.Tag!, StringComparer.OrdinalIgnoreCase);

            foreach (var group in byTag)
            {
                foreach (var old in group.Skip(RetentionByTag[group.Key]))
                {
                    try
                    {
                        File.Delete(old.Path);
                    }
                    catch
                    {
                        // Ignore deletion failure on individual file
                    }
                }
            }
        }
        catch
        {
            // Ignore directory enumeration failures
        }
    }

    private static string? ParseTag(string path)
    {
        var match = BackupFileName().Match(Path.GetFileNameWithoutExtension(path));
        return match.Success ? match.Groups["tag"].Value : null;
    }

    [GeneratedRegex(@"^tidyup_(?<tag>.+?)_\d{8}(_\d{6})?$")]
    private static partial Regex BackupFileName();
}

