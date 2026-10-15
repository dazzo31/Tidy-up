using System.IO;

namespace TidyUp.Services.Folders;

/// <summary>
/// Probes directory existence, read/write permissions, and system protection.
/// Supports injection of test delegates for deterministic unit testing.
/// </summary>
public class FolderValidator : IFolderValidator
{
    private readonly Func<string, bool> _directoryExistsCheck;
    private readonly Action<string>? _readProbe;
    private readonly Action<string>? _writeProbe;

    public FolderValidator(
        Func<string, bool>? directoryExistsCheck = null,
        Action<string>? readProbe = null,
        Action<string>? writeProbe = null)
    {
        _directoryExistsCheck = directoryExistsCheck ?? Directory.Exists;
        _readProbe = readProbe;
        _writeProbe = writeProbe;
    }

    public FolderValidationReport Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new FolderValidationReport
            {
                Path = path ?? string.Empty,
                Exists = false,
                Status = FolderValidationStatus.InvalidPath,
                Message = "Folder path cannot be empty."
            };
        }

        try
        {
            var isProtected = IsSystemProtectedDirectory(path);

            if (!_directoryExistsCheck(path))
            {
                return new FolderValidationReport
                {
                    Path = path,
                    Exists = false,
                    HasReadPermission = false,
                    HasWritePermission = false,
                    IsSystemProtected = isProtected,
                    Status = FolderValidationStatus.Missing,
                    Message = "Directory does not exist."
                };
            }

            var hasRead = CheckReadPermission(path);
            var hasWrite = hasRead && CheckWritePermission(path);

            if (!hasRead)
            {
                return new FolderValidationReport
                {
                    Path = path,
                    Exists = true,
                    HasReadPermission = false,
                    HasWritePermission = false,
                    IsSystemProtected = isProtected,
                    Status = FolderValidationStatus.AccessDenied,
                    Message = "Access denied: unable to read directory contents."
                };
            }

            var message = (hasRead, hasWrite) switch
            {
                (true, true) => "Read / Write (Full Access)",
                (true, false) => "Read Only (Write access unavailable)",
                _ => "Access denied"
            };

            return new FolderValidationReport
            {
                Path = path,
                Exists = true,
                HasReadPermission = hasRead,
                HasWritePermission = hasWrite,
                IsSystemProtected = isProtected,
                Status = FolderValidationStatus.Accessible,
                Message = message
            };
        }
        catch (Exception ex)
        {
            return new FolderValidationReport
            {
                Path = path,
                Exists = false,
                HasReadPermission = false,
                HasWritePermission = false,
                Status = FolderValidationStatus.InvalidPath,
                Message = $"Validation error: {ex.Message}"
            };
        }
    }

    private bool CheckReadPermission(string path)
    {
        try
        {
            if (_readProbe != null)
            {
                _readProbe(path);
                return true;
            }

            _ = Directory.EnumerateFileSystemEntries(path).FirstOrDefault();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool CheckWritePermission(string path)
    {
        try
        {
            if (_writeProbe != null)
            {
                _writeProbe(path);
                return true;
            }

            var testFile = Path.Combine(path, $".tidyup_perm_test_{Guid.NewGuid():N}.tmp");
            using (var fs = File.Create(testFile, 1, FileOptions.DeleteOnClose))
            {
                // Write access confirmed
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsSystemProtectedDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var full = Path.GetFullPath(path).TrimEnd('\\', '/');
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
            var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
            var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\', '/');

            if (!string.IsNullOrEmpty(winDir) &&
                (full.Equals(winDir, StringComparison.OrdinalIgnoreCase) ||
                 full.StartsWith(winDir + "\\", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(progFiles) &&
                (full.Equals(progFiles, StringComparison.OrdinalIgnoreCase) ||
                 full.StartsWith(progFiles + "\\", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(progFilesX86) &&
                (full.Equals(progFilesX86, StringComparison.OrdinalIgnoreCase) ||
                 full.StartsWith(progFilesX86 + "\\", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

