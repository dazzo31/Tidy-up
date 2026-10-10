using System.Diagnostics;
using System.IO;

namespace TidyUp.Services.FileSystem;

/// <summary>
/// Pre-flight service for detecting file locking, in-progress downloads,
/// and incomplete temporary files before initiating consequential file operations.
/// </summary>
public class FileLockDetector : IFileLockDetector
{
    private static readonly HashSet<string> IncompleteExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload",
        ".part",
        ".partial",
        ".download",
        ".tmp",
        ".temp"
    };

    /// <inheritdoc />
    public bool IsTemporaryOrIncompleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName))
            return false;

        // Office lock files and temporary editor files
        if (fileName.StartsWith("~$", StringComparison.Ordinal) ||
            fileName.StartsWith("~", StringComparison.Ordinal) ||
            fileName.StartsWith(".", StringComparison.Ordinal))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName);
        if (IncompleteExtensions.Contains(extension))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public bool IsFileReady(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        if (!File.Exists(filePath))
            return false;

        if (IsTemporaryOrIncompleteFile(filePath))
            return false;

        try
        {
            // Test exclusive read/write access (FileShare.None).
            // If another process holds an open write or read handle without sharing, this throws IOException.
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // File might have the ReadOnly attribute set. Check whether exclusive read access is possible.
            try
            {
                var attributes = File.GetAttributes(filePath);
                if (attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    using var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> WaitForFileReadyAsync(
        string filePath,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsFileReady(filePath))
                return true;

            var remaining = timeout - sw.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;

            var delay = remaining < pollInterval ? remaining : pollInterval;
            await Task.Delay(delay, cancellationToken);
        }

        return IsFileReady(filePath);
    }
}

