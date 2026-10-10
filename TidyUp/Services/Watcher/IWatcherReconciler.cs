namespace TidyUp.Services.Watcher;

/// <summary>
/// Service for performing reconciliation scans on monitored directories to recover
/// from dropped FileSystemWatcher events or buffer overflows.
/// </summary>
public interface IWatcherReconciler
{
    /// <summary>
    /// Event raised when a reconciliation scan is triggered for a directory.
    /// </summary>
    event EventHandler<string>? ReconciliationTriggered;

    /// <summary>
    /// Reconciles a folder by scanning all existing files and forwarding them for processing.
    /// </summary>
    Task ReconcileFolderAsync(
        string folderPath,
        bool includeSubfolders,
        IReadOnlyList<string> exclusionPatterns,
        CancellationToken cancellationToken = default);
}

