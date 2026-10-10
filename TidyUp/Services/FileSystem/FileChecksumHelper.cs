using System.IO;
using System.Security.Cryptography;

namespace TidyUp.Services.FileSystem;

/// <summary>
/// Helper for computing and validating SHA-256 checksums of filesystem files.
/// </summary>
public static class FileChecksumHelper
{
    /// <summary>
    /// Computes the lowercase SHA-256 hexadecimal hash string of the specified file.
    /// Returns string.Empty if the file does not exist or is inaccessible.
    /// </summary>
    public static string ComputeSha256(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return string.Empty;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Verifies that the file at the specified path exists and has a SHA-256 hash matching expectedHash.
    /// </summary>
    public static bool VerifySha256(string? filePath, string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(expectedHash))
            return false;

        var actualHash = ComputeSha256(filePath);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}

