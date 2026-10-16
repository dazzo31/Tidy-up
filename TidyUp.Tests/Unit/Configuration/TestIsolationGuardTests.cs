using System.Text.RegularExpressions;
using FluentAssertions;
using TidyUp.Configuration;
using Xunit;

namespace TidyUp.Tests.Unit.Configuration;

/// <summary>
/// Structural guards that keep the test suite away from the developer's real %APPDATA%\TidyUp.
/// Rationale: five unit-test classes once resolved the live database path, and rules written by
/// an early integration-test iteration were left in a real profile.
/// </summary>
public class TestIsolationGuardTests
{
    private static readonly Regex[] ForbiddenPatterns =
    [
        new(@"SpecialFolder\.ApplicationData", RegexOptions.Compiled),
        new(@"TidyUpPaths\.Default\(", RegexOptions.Compiled),
        new(@"ServiceConfiguration\.ConfigureServices\(\s*\)", RegexOptions.Compiled),
        new(@"new\s+SettingsRepository\(\s*\)", RegexOptions.Compiled)
    ];

    private static readonly string[] AllowedFiles =
    [
        "TidyUpPathsTests.cs",
        "TestIsolationGuardTests.cs"
    ];

    [Fact]
    public void ProcessEnvironment_RedirectsAppDataToTemp()
    {
        var resolved = TidyUpPaths.FromEnvironmentOrDefault().AppDataRoot;

        var realRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TidyUp");

        resolved.Should().NotBe(realRoot);
        resolved.Should().StartWith(Path.GetFullPath(Path.GetTempPath()));
    }

    [Fact]
    public void TestSources_NeverReferenceTheRealAppDataLocation()
    {
        var testProjectDir = FindTestProjectDirectory();

        var offenders = Directory
            .EnumerateFiles(testProjectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !AllowedFiles.Contains(Path.GetFileName(f)))
            .SelectMany(f => File.ReadAllLines(f)
                .Select((line, index) => (File: f, Line: index + 1, Text: line))
                .Where(x => ForbiddenPatterns.Any(p => p.IsMatch(x.Text))))
            .Select(x => $"{Path.GetRelativePath(testProjectDir, x.File)}:{x.Line}: {x.Text.Trim()}")
            .ToList();

        offenders.Should().BeEmpty(
            "tests must build services with TestServices.Create(IsolatedAppData) and an explicit TidyUpPaths");
    }

    private static string FindTestProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TidyUp.Tests.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("TidyUp.Tests.csproj not found above the test binaries.");
    }
}
