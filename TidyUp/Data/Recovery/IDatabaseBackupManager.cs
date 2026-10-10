namespace TidyUp.Data.Recovery;

public interface IDatabaseBackupManager
{
    string DefaultDatabasePath { get; }
    string BackupsDirectory { get; }

    Task<string?> CreateBackupAsync(string? sourceDbPath = null, string? destinationPath = null, string? tag = null);
    Task<string?> EnsureDailyBackupAsync(string? sourceDbPath = null);
    Task<bool> RestoreBackupAsync(string backupFilePath, string? targetDbPath = null);
    List<string> GetAvailableBackups();
    string? GetLatestBackup();
}

