using System.IO;

namespace TidyUp.Utilities;

/// <summary>
/// Validates and sanitizes file system paths to prevent directory traversal and invalid operations.
/// </summary>
public static class PathValidator
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Validates that a path is safe for file operations.
    /// </summary>
    public static PathValidationResult Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return PathValidationResult.Fail("Path cannot be empty.");

        // Check for directory traversal
        if (path.Contains(".."))
            return PathValidationResult.Fail("Path cannot contain '..' (directory traversal).");

        // Check for invalid characters
        var invalidChars = Path.GetInvalidPathChars();
        if (path.IndexOfAny(invalidChars) >= 0)
            return PathValidationResult.Fail("Path contains invalid characters.");

        // Check for reserved device names
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (!string.IsNullOrEmpty(fileName) && ReservedNames.Contains(fileName))
            return PathValidationResult.Fail($"'{fileName}' is a reserved Windows device name.");

        // Check path length (Windows MAX_PATH)
        if (path.Length > 260 && !path.StartsWith(@"\\?\"))
            return PathValidationResult.Warn("Path exceeds 260 characters. Long path support may be required.");

        return PathValidationResult.Ok();
    }

    /// <summary>
    /// Validates a file name (not a full path).
    /// </summary>
    public static PathValidationResult ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return PathValidationResult.Fail("File name cannot be empty.");

        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.IndexOfAny(invalidChars) >= 0)
            return PathValidationResult.Fail("File name contains invalid characters.");

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (ReservedNames.Contains(nameWithoutExt))
            return PathValidationResult.Fail($"'{nameWithoutExt}' is a reserved Windows device name.");

        return PathValidationResult.Ok();
    }

    /// <summary>
    /// Sanitizes a string for use as a file name by replacing invalid characters.
    /// </summary>
    public static string SanitizeFileName(string fileName, char replacement = '_')
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = fileName;

        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, replacement);
        }

        // Remove reserved names
        var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
        if (ReservedNames.Contains(nameWithoutExt))
        {
            sanitized = $"_{sanitized}";
        }

        return sanitized;
    }
}

/// <summary>
/// Result of a path validation check.
/// </summary>
public class PathValidationResult
{
    public bool IsValid { get; init; }
    public bool HasWarning { get; init; }
    public string? Message { get; init; }

    public static PathValidationResult Ok() => new() { IsValid = true };
    public static PathValidationResult Warn(string message) => new() { IsValid = true, HasWarning = true, Message = message };
    public static PathValidationResult Fail(string message) => new() { IsValid = false, Message = message };
}