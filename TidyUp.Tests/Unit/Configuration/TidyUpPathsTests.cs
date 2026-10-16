using FluentAssertions;
using TidyUp.Configuration;
using TidyUp.Data;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Configuration;

[Collection("EnvironmentVariables")]
public class TidyUpPathsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsEmptyRoot(string? root)
    {
        var act = () => new TidyUpPaths(root!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DerivedPaths_LiveUnderTheRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "TidyUp_PathsTest");

        var paths = new TidyUpPaths(root);

        paths.DatabasePath.Should().Be(Path.Combine(root, "tidyup.db"));
        paths.SettingsPath.Should().Be(Path.Combine(root, "settings.json"));
        paths.BackupsDirectory.Should().Be(Path.Combine(root, "Backups"));
        paths.ConnectionString.Should().Contain(paths.DatabasePath);
    }

    [Fact]
    public void ConnectionString_QuotesPathsContainingSemicolons()
    {
        var paths = new TidyUpPaths(Path.Combine(Path.GetTempPath(), "a;b"));

        paths.ConnectionString.Should().Contain("\"");
    }

    [Fact]
    public void Default_ResolvesUnderRoamingAppData()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TidyUp");

        TidyUpPaths.Default().AppDataRoot.Should().Be(expected);
    }

    [Fact]
    public void FromEnvironmentOrDefault_HonoursOverrideVariable()
    {
        var previous = Environment.GetEnvironmentVariable(TidyUpPaths.EnvironmentVariableName);
        var overrideRoot = Path.Combine(Path.GetTempPath(), $"TidyUp_EnvOverride_{Guid.NewGuid():N}");

        try
        {
            Environment.SetEnvironmentVariable(TidyUpPaths.EnvironmentVariableName, overrideRoot);

            TidyUpPaths.FromEnvironmentOrDefault().AppDataRoot.Should().Be(overrideRoot);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TidyUpPaths.EnvironmentVariableName, previous);
        }
    }

    [Fact]
    public void FromEnvironmentOrDefault_FallsBackToDefaultWhenVariableUnset()
    {
        var previous = Environment.GetEnvironmentVariable(TidyUpPaths.EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(TidyUpPaths.EnvironmentVariableName, null);

            TidyUpPaths.FromEnvironmentOrDefault().Should().Be(TidyUpPaths.Default());
        }
        finally
        {
            Environment.SetEnvironmentVariable(TidyUpPaths.EnvironmentVariableName, previous);
        }
    }

    [Fact]
    public async Task SettingsRepository_RoundTripsUnderTheSuppliedRoot_AndResolvesBackupPath()
    {
        using var appData = new IsolatedAppData();
        var repo = new SettingsRepository(appData.Paths);

        var defaults = await repo.LoadAsync();
        defaults.DatabaseBackupPath.Should().Be(appData.Paths.BackupsDirectory);

        defaults.ScanBatchSize = 123;
        await repo.SaveAsync(defaults);

        File.Exists(appData.Paths.SettingsPath).Should().BeTrue();
        (await repo.LoadAsync()).ScanBatchSize.Should().Be(123);
    }

    [Fact]
    public void ConfigureServices_WithExplicitPaths_CreatesDatabaseOnlyUnderThatRoot()
    {
        using var appData = new IsolatedAppData();

        var provider = TestServices.Create(appData);

        File.Exists(appData.Paths.DatabasePath).Should().BeTrue();
        (provider as IDisposable)?.Dispose();
    }
}
