using System.IO;

namespace TidyUp.Tests.Helpers;

/// <summary>
/// Helper class for creating and managing test files.
/// </summary>
public class TestFileHelper : IDisposable
{
    private readonly List<string> _createdFiles = new();
    private readonly List<string> _createdDirectories = new();
    private readonly string _testRootDirectory;

    public TestFileHelper()
    {
        _testRootDirectory = Path.Combine(Path.GetTempPath(), "TidyUpTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRootDirectory);
        _createdDirectories.Add(_testRootDirectory);
    }

    public string TestRootDirectory => _testRootDirectory;

    /// <summary>
    /// Creates a test file with optional content.
    /// </summary>
    public FileInfo CreateTestFile(string fileName, string? content = null, DateTime? createdDate = null, DateTime? modifiedDate = null)
    {
        var filePath = Path.Combine(_testRootDirectory, fileName);
        var directory = Path.GetDirectoryName(filePath);
        
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _createdDirectories.Add(directory);
        }

        File.WriteAllText(filePath, content ?? "test content");
        _createdFiles.Add(filePath);

        var fileInfo = new FileInfo(filePath);
        
        if (createdDate.HasValue)
        {
            File.SetCreationTime(filePath, createdDate.Value);
        }
        
        if (modifiedDate.HasValue)
        {
            File.SetLastWriteTime(filePath, modifiedDate.Value);
        }

        return fileInfo;
    }

    /// <summary>
    /// Creates a test directory.
    /// </summary>
    public DirectoryInfo CreateTestDirectory(string dirName)
    {
        var dirPath = Path.Combine(_testRootDirectory, dirName);
        var dirInfo = Directory.CreateDirectory(dirPath);
        _createdDirectories.Add(dirPath);
        return dirInfo;
    }

    /// <summary>
    /// Gets a path within the test root directory.
    /// </summary>
    public string GetTestPath(string relativePath)
    {
        return Path.Combine(_testRootDirectory, relativePath);
    }

    /// <summary>
    /// Checks if a file exists in the test directory.
    /// </summary>
    public bool FileExists(string relativePath)
    {
        return File.Exists(GetTestPath(relativePath));
    }

    /// <summary>
    /// Checks if a directory exists in the test directory.
    /// </summary>
    public bool DirectoryExists(string relativePath)
    {
        return Directory.Exists(GetTestPath(relativePath));
    }

    /// <summary>
    /// Gets FileInfo for a file in the test directory.
    /// </summary>
    public FileInfo GetFileInfo(string relativePath)
    {
        return new FileInfo(GetTestPath(relativePath));
    }

    public void Dispose()
    {
        // Clean up test files and directories
        foreach (var file in _createdFiles.Where(File.Exists))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        foreach (var dir in _createdDirectories.OrderByDescending(d => d.Length).Where(Directory.Exists))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
