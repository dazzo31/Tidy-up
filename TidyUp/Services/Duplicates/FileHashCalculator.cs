using System.IO;
using System.Security.Cryptography;

namespace TidyUp.Services.Duplicates;

public interface IFileHashCalculator
{
    string ComputePartialHash(string filePath, int bytesToRead = 4096);
    Task<string> ComputePartialHashAsync(string filePath, int bytesToRead = 4096, CancellationToken cancellationToken = default);
    string ComputeFullSha256(string filePath);
    Task<string> ComputeFullSha256Async(string filePath, CancellationToken cancellationToken = default);
}

public class FileHashCalculator : IFileHashCalculator
{
    public const int DefaultPartialBytes = 4096;

    public string ComputePartialHash(string filePath, int bytesToRead = DefaultPartialBytes)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return string.Empty;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[bytesToRead];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead <= 0)
                return "empty_file";

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(buffer, 0, bytesRead);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<string> ComputePartialHashAsync(string filePath, int bytesToRead = DefaultPartialBytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return string.Empty;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
            var buffer = new byte[bytesToRead];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

            if (bytesRead <= 0)
                return "empty_file";

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(buffer, 0, bytesRead);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    public string ComputeFullSha256(string filePath)
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

    public async Task<string> ComputeFullSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return string.Empty;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, FileOptions.Asynchronous);
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}

