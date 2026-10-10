namespace TidyUp.Services.Monitoring;

/// <summary>
/// Health and operational status of a monitored folder and its file system watcher.
/// </summary>
public enum WatcherHealthStatus
{
    /// <summary>
    /// Directory exists, is accessible, and watcher is actively listening for file events.
    /// </summary>
    Healthy,

    /// <summary>
    /// Monitoring is temporarily paused by the user or application state.
    /// </summary>
    Paused,

    /// <summary>
    /// Directory exists but watcher experienced an internal buffer overflow or performance degradation.
    /// </summary>
    Degraded,

    /// <summary>
    /// Watched path does not exist, removable drive was unplugged, or network share was severed.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Watched path encountered unrecoverable I/O permissions or operating system fault.
    /// </summary>
    Error
}

/// <summary>
/// Detailed health evaluation snapshot for a monitored directory.
/// </summary>
public class FolderHealthReport
{
    /// <summary>
    /// Fully qualified path of the monitored folder.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Current health status of the watcher.
    /// </summary>
    public WatcherHealthStatus Status { get; init; } = WatcherHealthStatus.Healthy;

    /// <summary>
    /// Human-readable explanation of current status, warnings, or error details.
    /// </summary>
    public string? StatusDetail { get; init; }

    /// <summary>
    /// UTC timestamp of the most recent health evaluation.
    /// </summary>
    public DateTime LastCheckedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when folder was last successfully accessed and confirmed operational.
    /// </summary>
    public DateTime? LastSuccessfulCheckUtc { get; init; }

    /// <summary>
    /// Number of consecutive accessibility or I/O failures.
    /// </summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>
    /// Indicates whether the monitored folder is a network share (UNC or mapped drive).
    /// </summary>
    public bool IsNetworkShare { get; init; }

    /// <summary>
    /// Next scheduled reconnection/retry delay based on backoff schedule.
    /// </summary>
    public TimeSpan NextRetryDelay { get; init; } = TimeSpan.Zero;
}

