using FluentAssertions;
using Moq;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Services;
using TidyUp.Services.Monitoring;
using TidyUp.ViewModels;

namespace TidyUp.Tests.Unit.Services;

public class WatcherHealthMonitorTests : IDisposable
{
    private readonly string _testTempDir;

    public WatcherHealthMonitorTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "TidyUp_HealthMonitorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void RegisterFolder_ExistingDirectory_InitializesAsHealthy()
    {
        // Arrange
        using var monitor = new WatcherHealthMonitor();

        // Act
        monitor.RegisterFolder(_testTempDir);
        var report = monitor.GetFolderHealth(_testTempDir);

        // Assert
        report.Should().NotBeNull();
        report!.Status.Should().Be(WatcherHealthStatus.Healthy);
        report.ConsecutiveFailures.Should().Be(0);
        report.StatusDetail.Should().Contain("accessible");
    }

    [Fact]
    public void RegisterFolder_NonExistentDirectory_InitializesAsDisconnected()
    {
        // Arrange
        var missingPath = Path.Combine(_testTempDir, "NonExistentSubfolder");
        using var monitor = new WatcherHealthMonitor();

        // Act
        monitor.RegisterFolder(missingPath);
        var report = monitor.GetFolderHealth(missingPath);

        // Assert
        report.Should().NotBeNull();
        report!.Status.Should().Be(WatcherHealthStatus.Disconnected);
        report.ConsecutiveFailures.Should().Be(1);
        report.NextRetryDelay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordDegradation_TransitionsToDegradedAndRaisesEvent()
    {
        // Arrange
        using var monitor = new WatcherHealthMonitor();
        monitor.RegisterFolder(_testTempDir);

        FolderHealthReport? eventReport = null;
        monitor.FolderHealthChanged += (s, e) => eventReport = e;

        // Act
        monitor.RecordDegradation(_testTempDir, "Internal buffer overflow 64KB");

        // Assert
        var report = monitor.GetFolderHealth(_testTempDir);
        report.Should().NotBeNull();
        report!.Status.Should().Be(WatcherHealthStatus.Degraded);
        report.StatusDetail.Should().Contain("overflow");

        eventReport.Should().NotBeNull();
        eventReport!.Status.Should().Be(WatcherHealthStatus.Degraded);
    }

    [Fact]
    public void ClearDegradation_RestoresHealthyStateAndRaisesEvent()
    {
        // Arrange
        using var monitor = new WatcherHealthMonitor();
        monitor.RegisterFolder(_testTempDir);
        monitor.RecordDegradation(_testTempDir, "Buffer overflow");

        FolderHealthReport? eventReport = null;
        monitor.FolderHealthChanged += (s, e) => eventReport = e;

        // Act
        monitor.ClearDegradation(_testTempDir);

        // Assert
        var report = monitor.GetFolderHealth(_testTempDir);
        report.Should().NotBeNull();
        report!.Status.Should().Be(WatcherHealthStatus.Healthy);

        eventReport.Should().NotBeNull();
        eventReport!.Status.Should().Be(WatcherHealthStatus.Healthy);
    }

    [Fact]
    public void SetFolderPaused_TransitionsBetweenPausedAndHealthy()
    {
        // Arrange
        using var monitor = new WatcherHealthMonitor();
        monitor.RegisterFolder(_testTempDir);

        // Act 1: Pause
        monitor.SetFolderPaused(_testTempDir, true);
        var pausedReport = monitor.GetFolderHealth(_testTempDir);

        // Assert 1
        pausedReport!.Status.Should().Be(WatcherHealthStatus.Paused);
        pausedReport.StatusDetail.Should().Contain("paused");

        // Act 2: Resume
        monitor.SetFolderPaused(_testTempDir, false);
        var resumedReport = monitor.GetFolderHealth(_testTempDir);

        // Assert 2
        resumedReport!.Status.Should().Be(WatcherHealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckAllHealthAsync_Reconnection_WhenDirectoryReturns_TransitionsToHealthy()
    {
        // Arrange: Start with non-existent directory simulated via delegate
        var isAvailable = false;
        using var monitor = new WatcherHealthMonitor(
            directoryExistsCheck: path => isAvailable,
            directoryAccessibilityProbe: path => { if (!isAvailable) throw new DirectoryNotFoundException(); });

        var folderPath = @"C:\MockShare\Documents";
        monitor.RegisterFolder(folderPath);

        monitor.GetFolderHealth(folderPath)!.Status.Should().Be(WatcherHealthStatus.Disconnected);

        FolderHealthReport? reconnectedReport = null;
        monitor.FolderHealthChanged += (s, e) => reconnectedReport = e;

        // Act: Share is plugged back in / reconnects
        isAvailable = true;
        await monitor.CheckFolderHealthAsync(folderPath);

        // Assert
        var currentReport = monitor.GetFolderHealth(folderPath);
        currentReport!.Status.Should().Be(WatcherHealthStatus.Healthy);
        currentReport.ConsecutiveFailures.Should().Be(0);

        reconnectedReport.Should().NotBeNull();
        reconnectedReport!.Status.Should().Be(WatcherHealthStatus.Healthy);
    }

    [Fact]
    public void CalculateBackoffDelay_CalculatesExponentialSchedule()
    {
        WatcherHealthMonitor.CalculateBackoffDelay(0).Should().Be(TimeSpan.FromSeconds(5));
        WatcherHealthMonitor.CalculateBackoffDelay(1).Should().Be(TimeSpan.FromSeconds(5));
        WatcherHealthMonitor.CalculateBackoffDelay(2).Should().Be(TimeSpan.FromSeconds(10));
        WatcherHealthMonitor.CalculateBackoffDelay(3).Should().Be(TimeSpan.FromSeconds(20));
        WatcherHealthMonitor.CalculateBackoffDelay(4).Should().Be(TimeSpan.FromSeconds(30));
        WatcherHealthMonitor.CalculateBackoffDelay(5).Should().Be(TimeSpan.FromSeconds(60));
        WatcherHealthMonitor.CalculateBackoffDelay(10).Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task CheckFolderHealthAsync_AccessDenied_ReportsError()
    {
        // Arrange
        using var monitor = new WatcherHealthMonitor(
            directoryExistsCheck: path => true,
            directoryAccessibilityProbe: path => throw new UnauthorizedAccessException("Access to folder is denied."));

        // Act
        var report = await monitor.CheckFolderHealthAsync(@"C:\RestrictedFolder");

        // Assert
        report.Status.Should().Be(WatcherHealthStatus.Error);
        report.StatusDetail.Should().Contain("Permission denied");
    }

    [Fact]
    public void IsNetworkPath_IdentifiesUncPathsCorrectly()
    {
        WatcherHealthMonitor.IsNetworkPath(@"\\server\share\folder").Should().BeTrue();
        WatcherHealthMonitor.IsNetworkPath(@"//server/share/folder").Should().BeTrue();
        WatcherHealthMonitor.IsNetworkPath(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void MonitoredFolderItem_HealthStatusProperties_ReflectState()
    {
        // Arrange
        var item = new MonitoredFolderItem
        {
            Path = _testTempDir
        };

        // Act & Assert Healthy
        item.UpdateHealth(WatcherHealthStatus.Healthy, "Operating normally");
        item.HealthStatusText.Should().Be("Healthy");
        item.HealthBadgeBackground.Should().NotBeNull();
        item.HealthBadgeForeground.Should().NotBeNull();

        // Act & Assert Degraded
        item.UpdateHealth(WatcherHealthStatus.Degraded, "Buffer overflow");
        item.HealthStatusText.Should().Be("Degraded");
        item.HealthTooltip.Should().Contain("Buffer overflow");

        // Act & Assert Disconnected
        item.UpdateHealth(WatcherHealthStatus.Disconnected, "Unreachable");
        item.HealthStatusText.Should().Be("Disconnected");
        item.HealthTooltip.Should().Contain("Unreachable");
    }

    [Fact]
    public async Task DashboardViewModel_OnFolderHealthChanged_UpdatesItemStatus()
    {
        // Arrange
        var mockRuleRepo = new Mock<IRuleRepository>();
        var mockActionLogRepo = new Mock<IActionLogRepository>();
        var mockFileMonitor = new Mock<IFileMonitorService>();
        using var healthMonitor = new WatcherHealthMonitor();

        mockRuleRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Rule>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Rule 1",
                    IsEnabled = true,
                    MonitoredFolders = new System.Collections.ObjectModel.ObservableCollection<MonitoredFolder>
                    {
                        new() { Path = _testTempDir, IncludeSubfolders = true }
                    }
                }
            });

        mockActionLogRepo.Setup(r => r.GetStatisticsAsync())
            .ReturnsAsync(new LogStatistics { Total = 0, Success = 0, Warning = 0, Error = 0 });
        mockActionLogRepo.Setup(r => r.GetFilteredAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<int>()))
            .ReturnsAsync(new List<TidyUp.Data.Entities.ActionLogEntity>());

        var vm = new DashboardViewModel(
            mockRuleRepo.Object,
            mockActionLogRepo.Object,
            mockFileMonitor.Object,
            watcherHealthMonitor: healthMonitor);

        await vm.LoadDashboardDataAsync();

        vm.MonitoredFolders.Should().HaveCount(1);
        var folderItem = vm.MonitoredFolders[0];
        folderItem.HealthStatus.Should().Be(WatcherHealthStatus.Healthy);

        // Act: Degrade via health monitor
        healthMonitor.RecordDegradation(_testTempDir, "Buffer overflow");

        // Assert: folderItem reflects degradation
        folderItem.HealthStatus.Should().Be(WatcherHealthStatus.Degraded);
        folderItem.HealthStatusText.Should().Be("Degraded");
    }

    [Fact]
    public async Task FileMonitorService_DegradationAndReconciliation_UpdatesHealthMonitor()
    {
        // Arrange
        using var healthMonitor = new WatcherHealthMonitor();
        using var service = new FileMonitorService(watcherHealthMonitor: healthMonitor);

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Monitor Test",
            IsEnabled = true,
            MonitoredFolders = new System.Collections.ObjectModel.ObservableCollection<MonitoredFolder>
            {
                new() { Path = _testTempDir, IncludeSubfolders = false }
            }
        };

        await service.StartAsync(new List<Rule> { rule });

        var initialReport = healthMonitor.GetFolderHealth(_testTempDir);
        initialReport.Should().NotBeNull();
        initialReport!.Status.Should().Be(WatcherHealthStatus.Healthy);

        // Act 1: Record degradation
        healthMonitor.RecordDegradation(_testTempDir, "Buffer overflow simulation");
        healthMonitor.GetFolderHealth(_testTempDir)!.Status.Should().Be(WatcherHealthStatus.Degraded);

        // Act 2: Reconcile folder (clears degradation)
        await service.ReconcileFolderAsync(_testTempDir, false, Array.Empty<string>());

        // Assert: Restored to Healthy
        healthMonitor.GetFolderHealth(_testTempDir)!.Status.Should().Be(WatcherHealthStatus.Healthy);

        await service.StopAsync();
    }

    [Fact]
    public void WatcherHealthMonitor_StartAndStop_TogglesRunningState()
    {
        // Arrange
        using var monitor = new WatcherHealthMonitor();
        monitor.IsRunning.Should().BeFalse();

        // Act
        monitor.Start();
        monitor.IsRunning.Should().BeTrue();

        monitor.Stop();
        monitor.IsRunning.Should().BeFalse();
    }
}
