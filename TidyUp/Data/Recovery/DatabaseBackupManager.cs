using System.IO;

namespace TidyUp.Data.Recovery;

public class DatabaseBackupManager : IDatabaseBackupManager
{
    private readonly string _defaultDatabasePath;
    private readonly string _backupsDirectory;

    public string DefaultDatabasePath => _defaultDatabasePath;
    public string BackupsDirectory => _backupsDirectory;

    public DatabaseBackupManager(string? databasePath = null, string? backupsDirectory = null)
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TidyUp");
        _defaultDatabasePath = databasePath ?? Path.Combine(appData, "tidyup.db");
        _backupsDirectory = backupsDirectory ?? Path.Combine(appData, "Backups");
    }

    public async Task<string?> CreateBackupAsync(string? sourceDbPath = null, string? destinationPath = null, string? tag = null)
    {
        var source = sourceDbPath ?? _defaultDatabasePath;
        if (!File.Exists(source))
            return null;

        Directory.CreateDirectory(_backupsDirectory);

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            var cleanTag = string.IsNullOrWhiteSpace(tag) ? "backup" : tag.Trim();
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            destinationPath = Path.Combine(_backupsDirectory, $"tidyup_{cleanTag}_{timestamp}.db");
        }
        else
        {
            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destDir))
                Directory.CreateDirectory(destDir);
        }

        // Safe file copy with read-share
        await using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        await using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await sourceStream.CopyToAsync(destStream);
        }

        PruneOldBackups(14);
        return destinationPath;
    }

    public async Task<string?> EnsureDailyBackupAsync(string? sourceDbPath = null)
    {
        var source = sourceDbPath ?? _defaultDatabasePath;
        if (!File.Exists(source))
            return null;

        Directory.CreateDirectory(_backupsDirectory);

        var todayTag = DateTime.UtcNow.ToString("yyyyMMdd");
        var expectedDailyFileName = $"tidyup_daily_{todayTag}.db";
        var dailyPath = Path.Combine(_backupsDirectory, expectedDailyFileName);

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

    private void PruneOldBackups(int keepCount)
    {
        try
        {
            var backups = GetAvailableBackups();
            if (backups.Count > keepCount)
            {
                foreach (var oldBackup in backups.Skip(keepCount))
                {
                    try
                    {
                        File.Delete(oldBackup);
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
}

