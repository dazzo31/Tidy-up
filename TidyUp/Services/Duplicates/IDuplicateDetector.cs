namespace TidyUp.Services.Duplicates;

public class DuplicateGroup
{
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string OriginalFile { get; set; } = string.Empty;
    public List<string> DuplicateFiles { get; set; } = [];
    public List<string> AllFiles { get; set; } = [];
}

public class DuplicateScanMetrics
{
    public int TotalFilesExamined { get; set; }
    public int SingletonsSkipped { get; set; }
    public int SizeCandidateGroups { get; set; }
    public int PartialHashesComputed { get; set; }
    public int PartialHashMatches { get; set; }
    public int FullHashesComputed { get; set; }
    public int DuplicateGroupsFound { get; set; }
    public int TotalDuplicateFilesFound { get; set; }
}

public class DuplicateScanResult
{
    public List<DuplicateGroup> Groups { get; set; } = [];
    public DuplicateScanMetrics Metrics { get; set; } = new();
}

public enum DuplicateAction
{
    Skip,
    MoveToDuplicatesFolder,
    DeleteToRecycleBin
}

public class DuplicateResolutionResult
{
    public int TotalProcessed { get; set; }
    public int MovedCount { get; set; }
    public int RecycledCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = [];
}

public interface IDuplicateDetector
{
    Task<DuplicateScanResult> FindDuplicatesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
    Task<DuplicateScanResult> ScanFolderAsync(string folderPath, bool searchSubdirectories = false, CancellationToken cancellationToken = default);
    Task<DuplicateResolutionResult> ResolveDuplicatesAsync(
        IEnumerable<DuplicateGroup> duplicateGroups,
        DuplicateAction action,
        string? customDuplicatesFolder = null,
        CancellationToken cancellationToken = default);
}

