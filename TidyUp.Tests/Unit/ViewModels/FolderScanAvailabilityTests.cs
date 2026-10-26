using FluentAssertions;
using TidyUp.Models.Enums;
using TidyUp.Models.Folders;
using TidyUp.Services.Folders;
using TidyUp.Services.Monitoring;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

/// <summary>
/// A Scan button on a folder that can't be read used to do nothing (or report nothing). It should be disabled and
/// say why, and the Folders grid cells should never look broken.
/// </summary>
public class FolderScanAvailabilityTests
{
    [Theory]
    [InlineData(FolderStatus.Healthy, true)]
    [InlineData(FolderStatus.Paused, true)]
    [InlineData(FolderStatus.Degraded, true)]
    [InlineData(FolderStatus.Missing, false)]
    [InlineData(FolderStatus.AccessDenied, false)]
    [InlineData(FolderStatus.Protected, false)]
    [InlineData(FolderStatus.Error, false)]
    public void CanScan_DependsOnStatus(FolderStatus status, bool expected)
    {
        FolderStatusPresentation.CanScan(status).Should().Be(expected);
    }

    [Fact]
    public void ScanTooltip_ExplainsWhyAScanIsUnavailable()
    {
        FolderStatusPresentation.ScanTooltip(FolderStatus.Healthy).Should().Be("Scan this folder now");
        FolderStatusPresentation.ScanTooltip(FolderStatus.Missing).Should().Contain("missing").And.Contain("Can't scan");
        FolderStatusPresentation.ScanTooltip(FolderStatus.AccessDenied).Should().Contain("access denied");
    }

    [Theory]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Healthy, false, true)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Paused, false, true)]
    [InlineData(FolderValidationStatus.Missing, WatcherHealthStatus.Disconnected, false, false)]
    [InlineData(FolderValidationStatus.AccessDenied, WatcherHealthStatus.Healthy, false, false)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Healthy, true, false)]
    [InlineData(FolderValidationStatus.Accessible, WatcherHealthStatus.Error, false, false)]
    public void DashboardItem_CanScan_FollowsTheFolderStatus(
        FolderValidationStatus validation, WatcherHealthStatus watcher, bool isProtected, bool expected)
    {
        var item = new MonitoredFolderItem { Path = @"C:\Temp\x", ValidationStatus = validation, IsSystemProtected = isProtected };
        item.UpdateHealth(watcher);

        item.CanScan.Should().Be(expected);
        if (!expected)
        {
            item.ScanTooltip.Should().StartWith("Can't scan");
        }
    }

    [Fact]
    public void DashboardItem_CanScan_IsFalseWhileScanning_AndRaisesChangeNotifications()
    {
        var item = new MonitoredFolderItem { Path = @"C:\Temp\x" };
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.IsScanning = true;

        item.CanScan.Should().BeFalse();
        item.ScanTooltip.Should().Be("Scan in progress");
        changed.Should().Contain(nameof(MonitoredFolderItem.CanScan));

        item.IsScanning = false;
        item.CanScan.Should().BeTrue();
    }

    [Fact]
    public void DashboardItem_CanScan_UpdatesWhenTheWatcherReportsAProblem()
    {
        var item = new MonitoredFolderItem { Path = @"C:\Temp\x" };
        item.CanScan.Should().BeTrue();

        item.UpdateHealth(WatcherHealthStatus.Disconnected, "Drive unplugged");

        item.CanScan.Should().BeFalse();
    }

    [Fact]
    public async Task DashboardScan_DoesNothingForAFolderThatCannotBeScanned()
    {
        var reconciler = new Moq.Mock<TidyUp.Services.Watcher.IWatcherReconciler>();
        var vm = new DashboardViewModel(
            new Moq.Mock<TidyUp.Data.Repositories.IRuleRepository>().Object,
            new Moq.Mock<TidyUp.Data.Repositories.IActionLogRepository>().Object,
            new Moq.Mock<TidyUp.Services.IFileMonitorService>().Object,
            reconciler.Object);
        var item = new MonitoredFolderItem { Path = Path.GetTempPath(), ValidationStatus = FolderValidationStatus.AccessDenied };

        await vm.ScanFolderAsync(item);

        item.IsScanning.Should().BeFalse();
        vm.StatusMessage.Should().NotStartWith("Scanning");
        reconciler.Verify(r => r.ReconcileFolderAsync(
            Moq.It.IsAny<string>(), Moq.It.IsAny<bool>(), Moq.It.IsAny<IReadOnlyList<string>>(),
            Moq.It.IsAny<CancellationToken>()), Moq.Times.Never);
    }

    [Fact]
    public void FoldersItem_ScopeText_NamesTheScope()
    {
        new MonitoredFolderItemViewModel { IncludeSubfolders = true }.RecursiveDisplay.Should().Be("Includes subfolders");
        new MonitoredFolderItemViewModel { IncludeSubfolders = false }.RecursiveDisplay.Should().Be("Top-level only");
    }

    [Fact]
    public void FoldersItem_Exclusions_ShowAnEmDashWhenEmpty_AndTheListOtherwise()
    {
        var item = new MonitoredFolderItemViewModel();
        item.ExclusionsDisplay.Should().Be("\u2014");

        item.ExclusionPatternsText = "node_modules, .git";
        item.ExclusionsDisplay.Should().Be("node_modules, .git");

        item.ExclusionPatternsText = "  ";
        item.ExclusionsDisplay.Should().Be("\u2014");
    }

    [Fact]
    public void FoldersItem_ScopeAndExclusionsNotifyWhenTheirSourcesChange()
    {
        var item = new MonitoredFolderItemViewModel();
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.IncludeSubfolders = false;
        item.ExclusionPatternsText = ".git";

        changed.Should().Contain(nameof(MonitoredFolderItemViewModel.RecursiveDisplay));
        changed.Should().Contain(nameof(MonitoredFolderItemViewModel.ExclusionsDisplay));
    }

    [Theory]
    [InlineData(false, false, false)]   // does not exist
    [InlineData(true, true, true)]      // protected system path
    public void FoldersItem_CanScan_IsFalseForUnreadableFolders(bool exists, bool isProtected, bool expectedStatusIsProblem)
    {
        var item = new MonitoredFolderItemViewModel
        {
            Exists = exists,
            HasReadPermission = true,
            HasWritePermission = true,
            IsSystemProtected = isProtected
        };

        item.RefreshStatus();

        item.CanScan.Should().BeFalse();
        item.ScanTooltip.Should().StartWith("Can't scan");
        expectedStatusIsProblem.Should().Be(isProtected);
    }

    [Fact]
    public void FoldersItem_CanScan_IsFalseWhileScanning_AndRaisesChangeNotifications()
    {
        var item = new MonitoredFolderItemViewModel { Exists = true, HasReadPermission = true, HasWritePermission = true };
        item.RefreshStatus();
        item.CanScan.Should().BeTrue();

        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.IsScanning = true;

        item.CanScan.Should().BeFalse();
        item.ScanTooltip.Should().Be("Scan in progress");
        changed.Should().Contain(nameof(MonitoredFolderItemViewModel.CanScan));
        changed.Should().Contain(nameof(MonitoredFolderItemViewModel.ScanTooltip));

        item.IsScanning = false;
        item.CanScan.Should().BeTrue();
    }

    [Fact]
    public void FoldersItem_CanScan_IsTrueForAHealthyAndForAPausedFolder()
    {
        var item = new MonitoredFolderItemViewModel { Exists = true, HasReadPermission = true, HasWritePermission = true };

        item.RefreshStatus();
        item.CanScan.Should().BeTrue();

        item.IsPaused = true;
        item.RefreshStatus();
        item.Status.Should().Be(FolderStatus.Paused);
        item.CanScan.Should().BeTrue();
    }
}
