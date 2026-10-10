using System.IO;
using System.Text.RegularExpressions;

namespace TidyUp.Services.Conditions;

public interface IContentConditionEvaluator
{
    bool Evaluate(string filePath, string pattern, bool isRegex = false, bool caseSensitive = false, int maxScanBytes = 1_048_576);
    Task<bool> EvaluateAsync(string filePath, string pattern, bool isRegex = false, bool caseSensitive = false, int maxScanBytes = 1_048_576, CancellationToken cancellationToken = default);
}

public class ContentConditionEvaluator : IContentConditionEvaluator
{
    public const int DefaultMaxScanBytes = 1_048_576; // 1 MB scan window to prevent OOM on multi-GB files

    public bool Evaluate(string filePath, string pattern, bool isRegex = false, bool caseSensitive = false, int maxScanBytes = DefaultMaxScanBytes)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || string.IsNullOrEmpty(pattern))
            return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096);
            return EvaluateStream(stream, pattern, isRegex, caseSensitive, maxScanBytes);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EvaluateAsync(string filePath, string pattern, bool isRegex = false, bool caseSensitive = false, int maxScanBytes = DefaultMaxScanBytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || string.IsNullOrEmpty(pattern))
            return false;

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
            return await Task.Run(() => EvaluateStream(stream, pattern, isRegex, caseSensitive, maxScanBytes), cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private static bool EvaluateStream(Stream stream, string pattern, bool isRegex, bool caseSensitive, int maxScanBytes)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        Regex? regex = null;
        if (isRegex)
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            regex = new Regex(pattern, options, TimeSpan.FromSeconds(2));
        }

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        long bytesRead = 0;
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            bytesRead += line.Length + Environment.NewLine.Length;

            if (isRegex)
            {
                if (regex != null && regex.IsMatch(line))
                    return true;
            }
            else
            {
                if (line.Contains(pattern, comparison))
                    return true;
            }

            if (bytesRead >= maxScanBytes)
                break;
        }

        return false;
    }
}

