using System.Reflection;

namespace TidyUp.Services.Diagnostics;

/// <summary>
/// One place that answers "which build am I running?" for the About card and bug reports.
/// Reads the assembly metadata written by the csproj (Version / InformationalVersion with the git SHA).
/// </summary>
public static class AppVersionInfo
{
    public static string Version { get; } = ResolveVersion();

    /// <summary>Short git SHA when the build stamped one, otherwise "unknown".</summary>
    public static string Commit { get; } = ResolveCommit();

    /// <summary>"1.2.0 (fb8b11f)" for display.</summary>
    public static string Display =>
        Commit == "unknown" ? Version : $"{Version} ({Commit})";

    private static string ResolveVersion()
    {
        var assembly = typeof(AppVersionInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // "1.2.0+<sha>" is SourceLink's format; the part before '+' is the human version.
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string ResolveCommit()
    {
        var informational = typeof(AppVersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var plus = informational?.IndexOf('+') ?? -1;
        if (plus < 0 || plus == (informational?.Length ?? 0) - 1)
        {
            return "unknown";
        }

        var sha = informational![(plus + 1)..].Trim();
        return sha.Length > 7 ? sha[..7] : sha;
    }
}
