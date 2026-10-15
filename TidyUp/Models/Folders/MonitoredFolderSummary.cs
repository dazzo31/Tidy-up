namespace TidyUp.Models.Folders;

/// <summary>
/// Aggregated summary metrics across all monitored folders.
/// </summary>
public class MonitoredFolderSummary
{
    public int TotalFolders { get; init; }
    public int HealthyFolders { get; init; }
    public int MissingFolders { get; init; }
    public int AccessDeniedFolders { get; init; }
    public int DegradedFolders { get; init; }
    public int PausedFolders { get; init; }

    /// <summary>
    /// Total count of distinct folders experiencing issues (missing, access denied, degraded, disconnected, or error).
    /// </summary>
    public int DistinctAlertCount { get; init; }
}

