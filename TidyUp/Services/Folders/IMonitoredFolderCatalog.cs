using System.Collections.ObjectModel;
using TidyUp.Models.Folders;

namespace TidyUp.Services.Folders;

/// <summary>
/// Event arguments for catalog updates.
/// </summary>
public class FolderCatalogChangedEventArgs : EventArgs
{
    public IReadOnlyList<MonitoredFolderState> Folders { get; init; } = [];
    public MonitoredFolderSummary Summary { get; init; } = new();
}

/// <summary>
/// Central application service that acts as the single source of truth for monitored folders.
/// Coordinates path normalization, filesystem existence/permission checks, and watcher health.
/// </summary>
public interface IMonitoredFolderCatalog : IDisposable
{
    /// <summary>
    /// Observable collection of distinct monitored folders, updated on the UI thread.
    /// </summary>
    ReadOnlyObservableCollection<MonitoredFolderState> Folders { get; }

    /// <summary>
    /// Current summary statistics across all monitored folders.
    /// </summary>
    MonitoredFolderSummary Summary { get; }

    /// <summary>
    /// Event raised whenever the catalog contents or health states change.
    /// </summary>
    event EventHandler<FolderCatalogChangedEventArgs>? CatalogChanged;

    /// <summary>
    /// Reloads rules from storage, canonicalizes all paths, validates folders,
    /// and synchronizes watcher registrations.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a monitored folder by path (normalized automatically).
    /// </summary>
    MonitoredFolderState? GetFolder(string path);

    /// <summary>
    /// Persists a standalone monitored folder to the catalog.
    /// </summary>
    Task<MonitoredFolderState> AddStandaloneFolderAsync(
        string path,
        bool includeSubfolders = true,
        IEnumerable<string>? exclusionPatterns = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a configured standalone monitored folder from the catalog.
    /// </summary>
    Task<bool> RemoveStandaloneFolderAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the persistent paused state for a monitored folder.
    /// </summary>
    Task SetFolderPausedAsync(string path, bool isPaused, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a folder path is explicitly paused in the catalog.
    /// </summary>
    bool IsFolderExplicitlyPaused(string path);
}

