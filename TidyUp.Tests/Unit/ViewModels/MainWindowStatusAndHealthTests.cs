using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TidyUp.Data.Repositories;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Monitoring;
using TidyUp.Services.State;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class MainWindowStatusAndHealthTests
{
    private readonly Mock<IRuleRepository> _ruleRepoMock = new();
    private readonly Mock<IImportExportService> _importExportMock = new();
    private readonly Mock<IWatcherHealthMonitor> _healthMonitorMock = new();
    private readonly IApplicationStateManager _stateManager = new ApplicationStateManager();
    private readonly IServiceProvider _serviceProvider;

    public MainWindowStatusAndHealthTests()
    {
        _serviceProvider = ServiceConfiguration.ConfigureServices();
    }

    private MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(
            _ruleRepoMock.Object,
            _importExportMock.Object,
            _serviceProvider,
            _stateManager,
            _healthMonitorMock.Object);
    }

    [Fact]
    public void StatusBadge_WhenIdleAndNoAlerts_ShowsIdleState()
    {
        _healthMonitorMock.Setup(m => m.GetAllFolderHealth())
            .Returns(new Dictionary<string, FolderHealthReport>());

        var vm = CreateViewModel();

        vm.StateBadgeText.Should().Be("IDLE");
        vm.HasActiveAlerts.Should().BeFalse();
        vm.AlertCount.Should().Be(0);
        vm.StateBadgeTooltip.Should().Contain("Application is idle");
    }

    [Fact]
    public void StatusBadge_WhenMonitoringRunningAndNoAlerts_ShowsMonitoringActive()
    {
        _healthMonitorMock.Setup(m => m.GetAllFolderHealth())
            .Returns(new Dictionary<string, FolderHealthReport>
            {
                ["C:\\Watched"] = new FolderHealthReport { Path = "C:\\Watched", Status = WatcherHealthStatus.Healthy }
            });

        var vm = CreateViewModel();
        _stateManager.TransitionTo(ApplicationLifecycleState.MonitoringRunning);

        vm.StateBadgeText.Should().Be("MONITORING ACTIVE");
        vm.StateBadgeBackground.Should().Be("#E8F5E9");
        vm.HasActiveAlerts.Should().BeFalse();
        vm.StateBadgeTooltip.Should().Contain("File system monitoring is active and healthy");
    }

    [Fact]
    public void StatusBadge_WhenDegradedFolderDetected_UpdatesToDegradedStateAndShowsAlertBadge()
    {
        var healthDict = new Dictionary<string, FolderHealthReport>
        {
            ["C:\\Watched"] = new FolderHealthReport
            {
                Path = "C:\\Watched",
                Status = WatcherHealthStatus.Degraded,
                StatusDetail = "Buffer overflow occurred"
            }
        };

        _healthMonitorMock.Setup(m => m.GetAllFolderHealth())
            .Returns(healthDict);

        var vm = CreateViewModel();
        _stateManager.TransitionTo(ApplicationLifecycleState.MonitoringRunning);

        vm.UpdateHealthAlerts();

        vm.AlertCount.Should().Be(1);
        vm.HasActiveAlerts.Should().BeTrue();
        vm.AlertBadgeText.Should().Be("1 Alert");
        vm.AlertBadgeTooltip.Should().Contain("Buffer overflow occurred");
        vm.StateBadgeText.Should().Be("MONITORING (DEGRADED)");
        vm.StateBadgeBackground.Should().Be("#FFF3E0");
        vm.StateBadgeForeground.Should().Be("#E65100");
        vm.StateBadgeIcon.Should().Be("AlertCircle");
        vm.StateBadgeTooltip.Should().Contain("Buffer overflow occurred");
    }

    [Fact]
    public void StatusBadge_WhenDegradedFolderRecovers_ClearsAlertAndReturnsToHealthy()
    {
        var healthDict = new Dictionary<string, FolderHealthReport>
        {
            ["C:\\Watched"] = new FolderHealthReport
            {
                Path = "C:\\Watched",
                Status = WatcherHealthStatus.Degraded,
                StatusDetail = "Buffer overflow occurred"
            }
        };

        _healthMonitorMock.Setup(m => m.GetAllFolderHealth())
            .Returns(healthDict);

        var vm = CreateViewModel();
        _stateManager.TransitionTo(ApplicationLifecycleState.MonitoringRunning);
        vm.UpdateHealthAlerts();
        vm.HasActiveAlerts.Should().BeTrue();

        // Recovery
        healthDict["C:\\Watched"] = new FolderHealthReport
        {
            Path = "C:\\Watched",
            Status = WatcherHealthStatus.Healthy
        };

        vm.UpdateHealthAlerts();

        vm.AlertCount.Should().Be(0);
        vm.HasActiveAlerts.Should().BeFalse();
        vm.StateBadgeText.Should().Be("MONITORING ACTIVE");
        vm.StateBadgeBackground.Should().Be("#E8F5E9");
    }
}
