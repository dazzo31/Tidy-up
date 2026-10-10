using System.IO;
using System.Security.Cryptography;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Rollback;

namespace TidyUp.Tests.Integration;

/// <summary>
/// Fixture providing isolated, fully cleanable temporary disk environments for end-to-end integration tests.
/// Ensures 100% cleanup of real files, handles read-only files, unicode names, and deep directories.
/// </summary>
public class TestDirectoryFixture : IDisposable
{
    private readonly string _rootDirectory;
    private bool _disposed;

    public string RootDirectory => _rootDirectory;
    public string SourceDirectory { get; }
    public string DestinationDirectory { get; }
    public string ArchiveDirectory { get; }

    public TestDirectoryFixture()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "TidyUp_Integration", Guid.NewGuid().ToString("N"));
        SourceDirectory = Path.Combine(_rootDirectory, "Source");
        DestinationDirectory = Path.Combine(_rootDirectory, "Destination");
        ArchiveDirectory = Path.Combine(_rootDirectory, "Archive");

        Directory.CreateDirectory(_rootDirectory);
        Directory.CreateDirectory(SourceDirectory);
        Directory.CreateDirectory(DestinationDirectory);
        Directory.CreateDirectory(ArchiveDirectory);
    }

    /// <summary>
    /// Creates a directory within the fixture root.
    /// </summary>
    public string CreateDirectory(string relativePath)
    {
        var fullPath = Path.Combine(_rootDirectory, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Creates a text file with specified content.
    /// </summary>
    public FileInfo CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_rootDirectory, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content);
        return new FileInfo(fullPath);
    }

    /// <summary>
    /// Creates an empty (0-byte) file.
    /// </summary>
    public FileInfo CreateZeroByteFile(string relativePath)
    {
        var fullPath = Path.Combine(_rootDirectory, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (File.Create(fullPath)) { }
        return new FileInfo(fullPath);
    }

    /// <summary>
    /// Creates a multi-megabyte binary file with a deterministic pattern.
    /// </summary>
    public FileInfo CreateLargeFile(string relativePath, int megabytes)
    {
        var fullPath = Path.Combine(_rootDirectory, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var buffer = new byte[1024 * 1024]; // 1 MB chunk
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i % 256);
        }

        using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            for (int i = 0; i < megabytes; i++)
            {
                stream.Write(buffer, 0, buffer.Length);
            }
        }

        return new FileInfo(fullPath);
    }

    /// <summary>
    /// Creates a file with complex Unicode characters in the filename.
    /// </summary>
    public FileInfo CreateUnicodeFile(string unicodeFileName, string content, string? subDirectory = null)
    {
        var parentDir = subDirectory != null ? Path.Combine(_rootDirectory, subDirectory) : SourceDirectory;
        Directory.CreateDirectory(parentDir);

        var fullPath = Path.Combine(parentDir, unicodeFileName);
        File.WriteAllText(fullPath, content);
        return new FileInfo(fullPath);
    }

    /// <summary>
    /// Creates a read-only file on disk.
    /// </summary>
    public FileInfo CreateReadOnlyFile(string relativePath, string content)
    {
        var fileInfo = CreateFile(relativePath, content);
        File.SetAttributes(fileInfo.FullName, FileAttributes.ReadOnly);
        return new FileInfo(fileInfo.FullName);
    }

    /// <summary>
    /// Computes the SHA256 checksum of a file within the fixture.
    /// </summary>
    public string ComputeChecksum(string relativeOrFullPath)
    {
        var path = Path.IsPathRooted(relativeOrFullPath)
            ? relativeOrFullPath
            : Path.Combine(_rootDirectory, relativeOrFullPath);

        return FileChecksumHelper.ComputeSha256(path);
    }

    /// <summary>
    /// Returns the full absolute path for a relative path within this fixture.
    /// </summary>
    public string GetFullPath(string relativePath)
    {
        return Path.Combine(_rootDirectory, relativePath);
    }

    /// <summary>
    /// Checks if a file exists relative to the fixture root.
    /// </summary>
    public bool FileExists(string relativeOrFullPath)
    {
        var path = Path.IsPathRooted(relativeOrFullPath)
            ? relativeOrFullPath
            : Path.Combine(_rootDirectory, relativeOrFullPath);

        return File.Exists(path);
    }

    /// <summary>
    /// Checks if a directory exists relative to the fixture root.
    /// </summary>
    public bool DirectoryExists(string relativeOrFullPath)
    {
        var path = Path.IsPathRooted(relativeOrFullPath)
            ? relativeOrFullPath
            : Path.Combine(_rootDirectory, relativeOrFullPath);

        return Directory.Exists(path);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (Directory.Exists(_rootDirectory))
        {
            CleanDirectoryRecursively(_rootDirectory);

            // Attempt deletion with short retry to allow OS handles to close
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(_rootDirectory))
                    {
                        Directory.Delete(_rootDirectory, recursive: true);
                    }
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException)
                {
                    CleanDirectoryRecursively(_rootDirectory);
                    Thread.Sleep(50);
                }
            }
        }

        GC.SuppressFinalize(this);
    }

    private static void CleanDirectoryRecursively(string directoryPath)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var attr = File.GetAttributes(file);
                    if ((attr & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(file, attr & ~FileAttributes.ReadOnly);
                    }
                }
                catch { }
            }

            foreach (var dir in Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var attr = File.GetAttributes(dir);
                    if ((attr & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(dir, attr & ~FileAttributes.ReadOnly);
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
