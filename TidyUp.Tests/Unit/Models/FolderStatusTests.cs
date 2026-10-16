using FluentAssertions;
using TidyUp.Models.Enums;
using TidyUp.Models.Folders;
using TidyUp.Services.Folders;
using TidyUp.Services.Monitoring;
using TidyUp.Styles;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.Models;

/// <summary>
/// The Dashboard and the Folders hub used to describe the same folder differently ("Disconnected" vs "Missing",
/// "Healthy" vs "Protected System Path"). These tests pin the single vocabulary.
/// </summary>
public class FolderStatusTests
{
    public static TheoryData<FolderValidationStatus, WatcherHealthStatus> AllPairs()
    {
        var data = new TheoryData<FolderValidationStatus, WatcherHealthStatus>();
        foreach (var v in Enum.GetValues<FolderValidationStatus>())
        {
            foreach (var w in Enum.GetValues<WatcherHealthStatus>())
            {
                data.Add(v, w);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Resolve_MapsEveryValidationAndWatcherPairToAStatus(FolderValidationStatus validation, WatcherHealthStatus watcher)
    {
        var status = FolderStatusResolver.Resolve(validation, watcher);

        Enum.IsDefined(status).Should().BeTrue();
    }

    [Theory]
    [InlineData(FolderValidationStatus.Missing, WatcherHealthStatus.Healthy, FolderStatus.Missing)]
    [InlineData(FolderValidationStatus.Missing, WatcherHealthStatus.Paused, FolderStatus.Missing)]
    [InlineData(FolderValidationStatus.AccessDenied, WatcherHealthStatus.Healthy, FolderStatus.AccessDenied)]
    [InlineData(FolderValidationStatus.InvalidPath, WatcherHealthStatus.Healthy, FolderStatus.Error)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Healthy, FolderStatus.Healthy)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Paused, FolderStatus.Paused)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Degraded, FolderStatus.Degraded)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Disconnected, FolderStatus.Missing)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Error, FolderStatus.Error)]
    public void Resolve_PathProblemsOutrankWatcherState(FolderValidationStatus validation, WatcherHealthStatus watcher, FolderStatus expected)
    {
        FolderStatusResolver.Resolve(validation, watcher).Should().Be(expected);
    }

    [Fact]
    public void Resolve_ProtectedSystemPath_IsReportedEvenWhenTheWatcherIsHealthy()
    {
        FolderStatusResolver.Resolve(FolderValidationStatus.Accessible, WatcherHealthStatus.Healthy, isSystemProtected: true)
            .Should().Be(FolderStatus.Protected);
    }

    [Fact]
    public void Resolve_MissingBeatsProtected()
    {
        FolderStatusResolver.Resolve(FolderValidationStatus.Missing, WatcherHealthStatus.Healthy, isSystemProtected: true)
            .Should().Be(FolderStatus.Missing);
    }

    [Theory]
    [InlineData(FolderStatus.Healthy)]
    [InlineData(FolderStatus.Paused)]
    [InlineData(FolderStatus.Degraded)]
    [InlineData(FolderStatus.Missing)]
    [InlineData(FolderStatus.AccessDenied)]
    [InlineData(FolderStatus.Protected)]
    [InlineData(FolderStatus.Error)]
    public void Presentation_DefinesWordingAndExistingTokensForEveryStatus(FolderStatus status)
    {
        FolderStatusPresentation.Text(status).Should().NotBeNullOrWhiteSpace();
        FolderStatusPresentation.Description(status).Should().NotBeNullOrWhiteSpace();

        var all = typeof(ThemeTokens).GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();
        all.Should().Contain(FolderStatusPresentation.BackgroundToken(status));
        all.Should().Contain(FolderStatusPresentation.ForegroundToken(status));
    }

    [Fact]
    public void Presentation_GivesEachStatusItsOwnWording()
    {
        Enum.GetValues<FolderStatus>().Select(FolderStatusPresentation.Text).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Healthy, false)]
    [InlineData(FolderValidationStatus.Missing, WatcherHealthStatus.Disconnected, false)]
    [InlineData(FolderValidationStatus.Missing, WatcherHealthStatus.Healthy, false)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Paused, false)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Healthy, true)]
    public void SameFolder_ShowsTheSameStatusOnDashboardAndFolders(FolderValidationStatus validation, WatcherHealthStatus watcher, bool protectedPath)
    {
        var state = new MonitoredFolderState { ValidationStatus = validation, WatcherHealth = watcher, IsSystemProtected = protectedPath };

        var dashboardItem = new MonitoredFolderItem { ValidationStatus = validation, IsSystemProtected = protectedPath };
        dashboardItem.UpdateHealth(watcher);

        dashboardItem.Status.Should().Be(state.Status);
    }
}
