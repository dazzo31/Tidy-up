using FluentAssertions;
using TidyUp.Services.Diagnostics;
using TidyUp.Tests.TestInfrastructure;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

/// <summary>
/// The About surface must not lie about the build it is part of.
/// </summary>
public class SettingsAboutTests
{
    private static SettingsViewModel CreateViewModel()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), "TidyUp_About_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsPath);
        return new SettingsViewModel(new TidyUp.Data.SettingsRepository(new TidyUp.Configuration.TidyUpPaths(settingsPath)));
    }

    [Fact]
    public void VersionText_IsNeverEmpty()
    {
        CreateViewModel().VersionText.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void VersionText_MatchesTheAssemblyMetadata()
    {
        var vm = CreateViewModel();

        // The csproj pins <Version>1.2.0</Version>, so the assembly version must agree with what we show.
        var assemblyVersion = typeof(AppVersionInfo).Assembly.GetName().Version!;
        assemblyVersion.ToString(3).Should().Be("1.2.0");
        vm.VersionText.Should().StartWith(assemblyVersion.ToString(3));
    }

    [Fact]
    public void VersionText_IncludesTheShortCommitWhenTheBuildStampedOne()
    {
        AppVersionInfo.Commit.Should().NotBeNullOrWhiteSpace();

        if (AppVersionInfo.Commit != "unknown")
        {
            AppVersionInfo.Commit.Should().MatchRegex("^[0-9a-f]{7,}$");
            AppVersionInfo.Display.Should().Be($"{AppVersionInfo.Version} ({AppVersionInfo.Commit})");
        }
        else
        {
            AppVersionInfo.Display.Should().Be(AppVersionInfo.Version);
        }
    }

    [Fact]
    public void Version_IsTheSemanticPartOnly_WithoutTheBuildMetadataSuffix()
    {
        AppVersionInfo.Version.Should().NotContain("+");
    }

    [Fact]
    public void RuntimeText_ReportsTheDotNetAndWindowsVersions()
    {
        var vm = CreateViewModel();

        vm.RuntimeText.Should().StartWith(".NET ").And.Contain("Windows");
    }

    [Fact]
    public void DiagnosticsBlock_CarriesVersionRuntimeAndDataPaths()
    {
        var vm = CreateViewModel();

        var text = vm.BuildDiagnostics();

        text.Should().Contain("TidyUp ").And.Contain("Runtime:").And.Contain("Data folder:")
            .And.Contain("Database:").And.Contain("Settings:");
        text.Split(Environment.NewLine).Should().HaveCount(5);
    }

    [Fact]
    public void CopyDiagnostics_ReportsInsteadOfThrowing_WhenTheClipboardIsUnavailable()
    {
        var vm = CreateViewModel();

        // Headless test host: no STA message pump, so the clipboard is unavailable by design.
        var act = () => vm.CopyDiagnosticsCommand.Execute(null);

        act.Should().NotThrow();
        vm.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AboutCard_IsInTheAdvancedSection_WithTheLogoSelfAndLinks()
    {
        var xaml = File.ReadAllText(AppSourceFiles.Find("Views/SettingsView.xaml"));
        var advancedSection = xaml[xaml.IndexOf("SECTION 5: ADVANCED", StringComparison.Ordinal)..];

        advancedSection.Should().Contain("About TidyUp");
        advancedSection.Should().Contain("controls:TidyUpLogo");
        advancedSection.Should().Contain("{Binding VersionText");
        advancedSection.Should().NotContain("FontSize=\"32\" FontWeight=\"Bold\" Text=\"TidyUp\"");
    }
}
