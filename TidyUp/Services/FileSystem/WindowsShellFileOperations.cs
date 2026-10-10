using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace TidyUp.Services.FileSystem;

/// <summary>
/// Windows-specific implementation of ISafeFileSystem leveraging Microsoft.VisualBasic.FileIO
/// and drive topology analysis to route deletions through the Windows Recycle Bin safely.
/// </summary>
public class WindowsShellFileOperations : ISafeFileSystem
{
    public bool CanSendToRecycleBin(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        // UNC paths (e.g., \\server\share\file.txt) do NOT support Windows Recycle Bin
        if (filePath.StartsWith(@"\\") || filePath.StartsWith("//"))
            return false;

        try
        {
            var root = Path.GetPathRoot(filePath);
            if (string.IsNullOrWhiteSpace(root))
                return false;

            var driveInfo = new DriveInfo(root);

            // Network drives mapped to drive letters (e.g., Z:\) do not support the Recycle Bin
            if (driveInfo.DriveType == DriveType.Network)
                return false;

            // Optical discs and RAM disks do not support the Recycle Bin
            if (driveInfo.DriveType is DriveType.CDRom or DriveType.Ram)
                return false;

            // Removable USB drives formatted as FAT, FAT32, or exFAT do not support Recycle Bin
            if (driveInfo.DriveType == DriveType.Removable &&
                !string.Equals(driveInfo.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return driveInfo.IsReady;
        }
        catch
        {
            // If drive cannot be queried or determined, conservatively assume Recycle Bin is unavailable
            return false;
        }
    }

    public Task DeleteFileSafelyAsync(
        string filePath,
        bool useRecycleBin = true,
        bool allowPermanentFallback = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (useRecycleBin)
        {
            if (!CanSendToRecycleBin(filePath))
            {
                if (!allowPermanentFallback)
                {
                    var root = Path.GetPathRoot(filePath);
                    throw new RecycleBinUnavailableException(
                        filePath,
                        "The target drive or volume does not support the Windows Recycle Bin (e.g. network share or unsupported volume).",
                        root);
                }

                // Explicit fallback permitted by caller
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                return Task.CompletedTask;
            }

            if (!File.Exists(filePath))
                return Task.CompletedTask;

            // Route through Windows Shell to Recycle Bin
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                filePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin);

            return Task.CompletedTask;
        }

        // Permanent deletion requested
        if (!allowPermanentFallback)
        {
            throw new InvalidOperationException(
                $"Permanent deletion of '{filePath}' was requested without explicit permanent fallback authorization.");
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}

