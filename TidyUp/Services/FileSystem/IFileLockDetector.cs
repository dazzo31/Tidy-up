namespace TidyUp.Services.FileSystem;

/// <summary>
/// Pre-flight service for detecting file locking, in-progress downloads,
/// and incomplete temporary files before initiating consequential file operations.
/// </summary>
public interface IFileLockDetector
{
    /// <summary>
    /// Checks whether the file name or extension represents an in-progress temporary download,
    /// partial file, or Office application lock file (e.g. .crdownload, .part, .tmp, ~$*).
    /// </summary>
    bool IsTemporaryOrIncompleteFile(string filePath);

    /// <summary>
    /// Tests whether the file exists and is completely available for exclusive access (FileShare.None).
    /// Returns true if no other process holds an open write handle.
    /// </summary>
    bool IsFileReady(string filePath);

    /// <summary>
    /// Asynchronously polls until the file becomes available or the specified timeout expires.
    /// </summary>
    Task<bool> WaitForFileReadyAsync(
        string filePath,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken = default);
}

