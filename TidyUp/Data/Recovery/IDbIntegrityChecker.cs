namespace TidyUp.Data.Recovery;

public class DbIntegrityResult
{
    public bool IsOk { get; set; }
    public List<string> Messages { get; set; } = [];
}

public class DatabaseStartupResult
{
    public bool Success { get; set; }
    public bool WasCorruptedInitially { get; set; }
    public bool RestoredFromBackup { get; set; }
    public string? BackupPathUsed { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IDbIntegrityChecker
{
    Task<DbIntegrityResult> CheckIntegrityAsync(string dbPath);
    Task<DatabaseStartupResult> SafeInitializeDatabaseAsync(string dbPath, Func<Task> migrationOrCreationAction);
}

