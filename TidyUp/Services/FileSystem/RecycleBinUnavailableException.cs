namespace TidyUp.Services.FileSystem;

/// <summary>
/// Exception thrown when a file deletion operation requires the Windows Recycle Bin,
/// but the destination drive/path does not support recycling (e.g., network shares, UNC paths, or removable drives).
/// </summary>
public class RecycleBinUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Path of the file that could not be safely recycled.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// The root drive or network prefix.
    /// </summary>
    public string? DriveRoot { get; }

    /// <summary>
    /// Reason why the Recycle Bin is unavailable.
    /// </summary>
    public string Reason { get; }

    public RecycleBinUnavailableException(string filePath, string reason, string? driveRoot = null)
        : base($"The file '{filePath}' cannot be safely sent to the Recycle Bin. {reason}")
    {
        FilePath = filePath;
        Reason = reason;
        DriveRoot = driveRoot;
    }
}

