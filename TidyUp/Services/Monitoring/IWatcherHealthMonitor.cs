namespace TidyUp.Services.Monitoring;

/// <summary>
/// Proactively monitors the accessibility, health, and degraded states of directories watched by TidyUp.
/// Detects unplugged USB drives, severed network shares, deleted folders, and watcher buffer overflows.
/// </summary>
public interface IWatcherHealthMonitor : IDisposable
{
    /// <summary>
    /// Event raised whenever a monitored folder transitions between health states.
    /// </summary>
    event EventHandler<FolderHealthReport>? FolderHealthChanged;

    /// <summary>
    /// Interval between automatic background health heartbeats. Default is 30 seconds.
    /// </summary>
    TimeSpan HeartbeatInterval { get; set; }

    /// <summary>
    /// Gets whether periodic health heartbeat checks are actively running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts periodic background heartbeat checks.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops periodic background heartbeat checks.
    /// </summary>
    void Stop();

    /// <summary>
    /// Registers a folder path to be monitored for health and accessibility.
    /// </summary>
    void RegisterFolder(string folderPath, bool isPaused = false);

    /// <summary>
    /// Unregisters a folder path from health monitoring.
    /// </summary>
    void UnregisterFolder(string folderPath);

    /// <summary>
    /// Marks a folder as Paused or Resumed.
    /// </summary>
    void SetFolderPaused(string folderPath, bool isPaused);

    /// <summary>
    /// Records a buffer overflow or performance degradation event for a watched directory.
    /// </summary>
    void RecordDegradation(string folderPath, string reason);

    /// <summary>
    /// Clears degradation status once reconciliation or recovery is complete.
    /// </summary>
    void ClearDegradation(string folderPath);

    /// <summary>
    /// Immediately checks health of all registered folders.
    /// </summary>
    Task CheckAllHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately checks health of a specific registered folder.
    /// </summary>
    Task<FolderHealthReport> CheckFolderHealthAsync(string folderPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent cached health report for a folder, or null if unregistered.
    /// </summary>
    FolderHealthReport? GetFolderHealth(string folderPath);

    /// <summary>
    /// Gets a snapshot of current health reports for all registered folders.
    /// </summary>
    IReadOnlyDictionary<string, FolderHealthReport> GetAllFolderHealth();
}

