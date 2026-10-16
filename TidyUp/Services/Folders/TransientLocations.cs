using System.IO;

namespace TidyUp.Services.Folders;

/// <summary>Detects folders Windows and cleanup tools treat as disposable (the user's Temp directory).</summary>
public static class TransientLocations
{
    public static bool IsTransient(string? path) => IsUnder(path, Path.GetTempPath());

    private static bool IsUnder(string? path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            return false;

        try
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

            return full.Equals(rootFull, StringComparison.OrdinalIgnoreCase)
                   || full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
