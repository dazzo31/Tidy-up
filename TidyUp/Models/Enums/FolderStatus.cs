namespace TidyUp.Models.Enums;

/// <summary>
/// The one vocabulary for a monitored folder's state, shared by the Dashboard, the Folders hub and the alert badge.
/// Derived from the physical check (<c>FolderValidationStatus</c>) and the watcher (<c>WatcherHealthStatus</c>) by
/// <see cref="Folders.FolderStatusResolver"/>.
/// </summary>
public enum FolderStatus
{
    /// <summary>Accessible and being watched.</summary>
    Healthy,

    /// <summary>Accessible, but monitoring is paused.</summary>
    Paused,

    /// <summary>Accessible, but the watcher overflowed or is falling behind.</summary>
    Degraded,

    /// <summary>The folder does not exist, or its drive/share is gone.</summary>
    Missing,

    /// <summary>The folder exists but TidyUp cannot read or write it.</summary>
    AccessDenied,

    /// <summary>A protected system location where TidyUp will not act.</summary>
    Protected,

    /// <summary>The path is invalid, or the watcher hit an unrecoverable fault.</summary>
    Error
}
