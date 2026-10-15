using System.IO;

namespace TidyUp.Services.Folders;

/// <summary>
/// Default implementation of IFolderPathNormalizer.
/// Resolves quotes, environment variables, relative dots, and trailing separators.
/// </summary>
public class FolderPathNormalizer : IFolderPathNormalizer
{
    public bool TryNormalize(string? input, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Path cannot be empty or whitespace.";
            return false;
        }

        var trimmed = input.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = "Path cannot be empty.";
            return false;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(trimmed);

            if (!Path.IsPathFullyQualified(expanded))
            {
                error = "Path must be fully qualified and absolute (e.g. 'C:\\Folder' or '\\\\server\\share').";
                return false;
            }

            var full = Path.GetFullPath(expanded);

            // Preserve drive and UNC roots: e.g. "C:\" or "\\server\share\"
            var root = Path.GetPathRoot(full);
            if (!string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                full = Path.TrimEndingDirectorySeparator(full);
            }

            normalized = full;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Invalid path format: {ex.Message}";
            return false;
        }
    }

    public string Normalize(string? input)
    {
        if (TryNormalize(input, out var normalized, out var error))
        {
            return normalized;
        }

        throw new ArgumentException(error ?? "Invalid path.", nameof(input));
    }

    public bool AreEqual(string? path1, string? path2)
    {
        var norm1 = TryNormalize(path1, out var n1, out _) ? n1 : path1?.Trim();
        var norm2 = TryNormalize(path2, out var n2, out _) ? n2 : path2?.Trim();

        return string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase);
    }
}

