namespace TidyUp.Services.Folders;

/// <summary>
/// Normalizes folder paths to canonical, absolute forms to prevent duplicate
/// registrations and path mismatches across ViewModels.
/// </summary>
public interface IFolderPathNormalizer
{
    /// <summary>
    /// Attempts to normalize a folder path.
    /// Expands environment variables, resolves relative segments, trims trailing separators
    /// (while preserving drive roots like "C:\"), and verifies that the path is absolute.
    /// </summary>
    bool TryNormalize(string? input, out string normalized, out string? error);

    /// <summary>
    /// Normalizes a folder path or throws an ArgumentException if the path is invalid.
    /// </summary>
    string Normalize(string? input);

    /// <summary>
    /// Compares two paths for logical equality in a canonical, case-insensitive manner.
    /// </summary>
    bool AreEqual(string? path1, string? path2);
}

