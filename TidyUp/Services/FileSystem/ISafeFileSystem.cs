namespace TidyUp.Services.FileSystem;

/// <summary>
/// Abstraction for safe filesystem interactions, enforcing Windows Shell Recycle Bin protections
/// and guarding against accidental permanent deletion on unsupported media.
/// </summary>
public interface ISafeFileSystem
{
    /// <summary>
    /// Checks whether the specified file path is located on a volume that supports the Windows Recycle Bin.
    /// UNC paths, mapped network shares, and non-NTFS removable drives typically return false.
    /// </summary>
    bool CanSendToRecycleBin(string filePath);

    /// <summary>
    /// Deletes a file safely using the Windows Shell Recycle Bin by default.
    /// If Recycle Bin is unavailable and allowPermanentFallback is false, throws RecycleBinUnavailableException.
    /// </summary>
    Task DeleteFileSafelyAsync(
        string filePath,
        bool useRecycleBin = true,
        bool allowPermanentFallback = false);
}

