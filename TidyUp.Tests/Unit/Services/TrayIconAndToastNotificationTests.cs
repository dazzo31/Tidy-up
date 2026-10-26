using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TidyUp.Configuration;
using TidyUp.Data;
using TidyUp.Models;
using TidyUp.Services;
using TidyUp.Services.Monitoring;
using TidyUp.Services.State;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class TrayIconAndToastNotificationTests
{
    [Fact]
    public void ToastNotificationService_DispatchesNotification_WhenEnabled()
    {
        // Arrange
        var mockTray = new Mock<ITrayIconService>();
        var settings = new AppSettings
        {
            ToastNotificationsEnabled = true,
            QuietMode = false
        };

        var service = new ToastNotificationService(settings, mockTray.Object);
        ToastNotificationEventArgs? receivedArgs = null;
        service.NotificationTriggered += (s, e) => receivedArgs = e;

        // Act
        service.ShowNotification("Test Title", "Test Message", ToastNotificationType.Information);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal("Test Title", receivedArgs.Title);
        Assert.Equal("Test Message", receivedArgs.Message);
        Assert.Equal(ToastNotificationType.Information, receivedArgs.Type);
        mockTray.Verify(t => t.ShowBalloonTip("Test Title", "Test Message", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info), Times.Once);
    }

    [Fact]
    public void ToastNotificationService_SuppressesNotifications_WhenQuietModeActive()
    {
        // Arrange
        var mockTray = new Mock<ITrayIconService>();
        var settings = new AppSettings
        {
            ToastNotificationsEnabled = true,
            QuietMode = true
        };

        var service = new ToastNotificationService(settings, mockTray.Object);
        bool triggered = false;
        service.NotificationTriggered += (s, e) => triggered = true;

        // Act
        service.ShowNotification("Suppressed", "Should not show", ToastNotificationType.Warning);

        // Assert
        Assert.False(triggered);
        mockTray.Verify(t => t.ShowBalloonTip(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Hardcodet.Wpf.TaskbarNotification.BalloonIcon>()), Times.Never);
    }

    [Fact]
    public void ToastNotificationService_SuppressesNotifications_WhenNotificationsDisabled()
    {
        // Arrange
        var mockTray = new Mock<ITrayIconService>();
        var settings = new AppSettings
        {
            ToastNotificationsEnabled = false,
            QuietMode = false
        };

        var service = new ToastNotificationService(settings, mockTray.Object);
        bool triggered = false;
        service.NotificationTriggered += (s, e) => triggered = true;

        // Act
        service.ShowNotification("Suppressed", "Should not show", ToastNotificationType.Error);

        // Assert
        Assert.False(triggered);
        mockTray.Verify(t => t.ShowBalloonTip(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Hardcodet.Wpf.TaskbarNotification.BalloonIcon>()), Times.Never);
    }

    [Fact]
    public void ToastNotificationService_SuppressesSuccess_WhenShowSuccessNotificationsFalse()
    {
        var mockTray = new Mock<ITrayIconService>();
        var settings = new AppSettings
        {
            ToastNotificationsEnabled = true,
            QuietMode = false,
            ShowSuccessNotifications = false
        };

        var service = new ToastNotificationService(settings, mockTray.Object);
        bool triggered = false;
        service.NotificationTriggered += (s, e) => triggered = true;

        service.ShowNotification("Success Title", "Success Message", ToastNotificationType.Success);

        Assert.False(triggered);
        mockTray.Verify(t => t.ShowBalloonTip(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Hardcodet.Wpf.TaskbarNotification.BalloonIcon>()), Times.Never);

        // Act & Assert when enabled
        settings.ShowSuccessNotifications = true;
        service.ShowNotification("Success Title", "Success Message", ToastNotificationType.Success);

        Assert.True(triggered);
        mockTray.Verify(t => t.ShowBalloonTip("Success Title", "Success Message", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info), Times.Once);
    }

    [Fact]
    public void ToastNotificationService_SuppressesError_WhenShowErrorNotificationsFalse()
    {
        var mockTray = new Mock<ITrayIconService>();
        var settings = new AppSettings
        {
            ToastNotificationsEnabled = true,
            QuietMode = false,
            ShowErrorNotifications = false
        };

        var service = new ToastNotificationService(settings, mockTray.Object);
        bool triggered = false;
        service.NotificationTriggered += (s, e) => triggered = true;

        service.ShowNotification("Error Title", "Error Message", ToastNotificationType.Error);

        Assert.False(triggered);
        mockTray.Verify(t => t.ShowBalloonTip(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Hardcodet.Wpf.TaskbarNotification.BalloonIcon>()), Times.Never);

        // Act & Assert when enabled
        settings.ShowErrorNotifications = true;
        service.ShowNotification("Error Title", "Error Message", ToastNotificationType.Error);

        Assert.True(triggered);
        mockTray.Verify(t => t.ShowBalloonTip("Error Title", "Error Message", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Error), Times.Once);
    }

    [Fact]
    public void ToastNotificationService_NeverThrows_WhenNotificationHandlerOrTrayFails()
    {
        var mockTray = new Mock<ITrayIconService>();
        mockTray.Setup(t => t.ShowBalloonTip(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Hardcodet.Wpf.TaskbarNotification.BalloonIcon>()))
            .Throws(new InvalidOperationException("Simulated Win32 shell failure"));

        var settings = new AppSettings
        {
            ToastNotificationsEnabled = true,
            QuietMode = false
        };

        var service = new ToastNotificationService(settings, mockTray.Object);
        service.NotificationTriggered += (s, e) => throw new ApplicationException("Simulated subscriber failure");

        // Action must not throw to caller
        var act = () => service.ShowNotification("Test", "Message", ToastNotificationType.Information);
        act.Should().NotThrow();
    }

    [Fact]
    public void ToastNotificationService_NotifyBatchCompletedWithErrors_FormatsProperly()
    {
        // Arrange
        var mockTray = new Mock<ITrayIconService>();
        var service = new ToastNotificationService(new AppSettings(), mockTray.Object);
        ToastNotificationEventArgs? receivedArgs = null;
        service.NotificationTriggered += (s, e) => receivedArgs = e;

        // Act
        service.NotifyBatchCompletedWithErrors("Clean Downloads", 3, 20);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal("Batch Completed with Errors", receivedArgs.Title);
        Assert.Contains("Clean Downloads", receivedArgs.Message);
        Assert.Contains("3 of 20", receivedArgs.Message);
        Assert.Equal(ToastNotificationType.Warning, receivedArgs.Type);
    }

    [Fact]
    public void ToastNotificationService_NotifyFolderDisconnected_FormatsProperly()
    {
        // Arrange
        var mockTray = new Mock<ITrayIconService>();
        var service = new ToastNotificationService(new AppSettings(), mockTray.Object);
        ToastNotificationEventArgs? receivedArgs = null;
        service.NotificationTriggered += (s, e) => receivedArgs = e;

        // Act
        service.NotifyFolderDisconnected(@"\\server\share\docs", "Network unreachable");

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal("Watched Folder Disconnected", receivedArgs.Title);
        Assert.Contains(@"\\server\share\docs", receivedArgs.Message);
        Assert.Contains("Network unreachable", receivedArgs.Message);
        Assert.Equal(ToastNotificationType.Error, receivedArgs.Type);
    }

    [Fact]
    public void TrayIconService_CreateStatusIcon_ReturnsValidIconsForAllStates()
    {
        // Act & Assert
        using var normalIcon = TrayIconService.CreateStatusIcon(TrayIconStatus.Normal);
        Assert.NotNull(normalIcon);

        using var pausedIcon = TrayIconService.CreateStatusIcon(TrayIconStatus.Paused);
        Assert.NotNull(pausedIcon);

        using var warningIcon = TrayIconService.CreateStatusIcon(TrayIconStatus.Warning);
        Assert.NotNull(warningIcon);
    }

    [Fact]
    public async Task TrayIconService_PauseAndResume_UpdatesStatusAndCoordinatesMonitor()
    {
        // Arrange
        var mockMonitor = new Mock<IFileMonitorService>();
        var stateManager = new ApplicationStateManager();
        stateManager.TryTransitionTo(ApplicationLifecycleState.MonitoringRunning);

        var service = new TrayIconService(fileMonitorService: mockMonitor.Object, stateManager: stateManager);

        // Act: Pause
        await service.PauseMonitoringAsync();

        // Assert
        Assert.Equal(TrayIconStatus.Paused, service.CurrentStatus);
        Assert.Equal(ApplicationLifecycleState.MonitoringPaused, stateManager.CurrentState);
        mockMonitor.Verify(m => m.Pause(), Times.Once);

        // Act: Resume
        await service.ResumeMonitoringAsync();

        // Assert
        Assert.Equal(TrayIconStatus.Normal, service.CurrentStatus);
        Assert.Equal(ApplicationLifecycleState.MonitoringRunning, stateManager.CurrentState);
        mockMonitor.Verify(m => m.ResumeAsync(), Times.Once);
    }

    [Fact]
    public void TrayIconService_WhenHealthDegrades_TransitionsToWarningStatus()
    {
        // Arrange
        var mockHealthMonitor = new Mock<IWatcherHealthMonitor>();
        var service = new TrayIconService(healthMonitor: mockHealthMonitor.Object);

        // Act: trigger Degraded event from health monitor
        mockHealthMonitor.Raise(m => m.FolderHealthChanged += null,
            mockHealthMonitor.Object,
            new FolderHealthReport
            {
                Path = @"C:\MonitoredFolder",
                Status = WatcherHealthStatus.Degraded,
                StatusDetail = "Buffer overflow"
            });

        // Assert
        Assert.Equal(TrayIconStatus.Warning, service.CurrentStatus);

        // Act: folder recovers to Healthy
        mockHealthMonitor.Raise(m => m.FolderHealthChanged += null,
            mockHealthMonitor.Object,
            new FolderHealthReport
            {
                Path = @"C:\MonitoredFolder",
                Status = WatcherHealthStatus.Healthy
            });

        // Assert
        Assert.Equal(TrayIconStatus.Normal, service.CurrentStatus);
    }

    [Fact]
    public void TrayIconService_AggregatesHealthAcrossMultipleFolders_WarningNotClearedByHealthySibling()
    {
        var mockHealthMonitor = new Mock<IWatcherHealthMonitor>();
        var service = new TrayIconService(healthMonitor: mockHealthMonitor.Object);

        // Folder A disconnects -> Warning
        mockHealthMonitor.Raise(m => m.FolderHealthChanged += null,
            mockHealthMonitor.Object,
            new FolderHealthReport
            {
                Path = @"C:\FolderA",
                Status = WatcherHealthStatus.Disconnected,
                StatusDetail = "Drive unplugged"
            });

        service.CurrentStatus.Should().Be(TrayIconStatus.Warning);

        // Folder B reports healthy -> Must REMAIN Warning because Folder A is still disconnected
        mockHealthMonitor.Raise(m => m.FolderHealthChanged += null,
            mockHealthMonitor.Object,
            new FolderHealthReport
            {
                Path = @"C:\FolderB",
                Status = WatcherHealthStatus.Healthy
            });

        service.CurrentStatus.Should().Be(TrayIconStatus.Warning);

        // Folder A recovers to healthy -> Returns to Normal
        mockHealthMonitor.Raise(m => m.FolderHealthChanged += null,
            mockHealthMonitor.Object,
            new FolderHealthReport
            {
                Path = @"C:\FolderA",
                Status = WatcherHealthStatus.Healthy
            });

        service.CurrentStatus.Should().Be(TrayIconStatus.Normal);
    }

    [Fact]
    public void TrayIconService_CreateStatusIconWithNativeHandle_GeneratesValidHandles()
    {
        var (icon, hIcon) = TrayIconService.CreateStatusIconWithNativeHandle(TrayIconStatus.Warning);
        try
        {
            icon.Should().NotBeNull();
            icon.Width.Should().Be(16);
            icon.Height.Should().Be(16);
            hIcon.Should().NotBe(IntPtr.Zero);
        }
        finally
        {
            icon.Dispose();
        }
    }

    [Fact]
    public void TrayIconService_WhenSystemTrayDisabled_SuppressesBalloonTips()
    {
        var settings = new AppSettings { SystemTrayEnabled = false };
        var service = new TrayIconService(settings: settings);

        // Balloon tip must not throw and must be suppressed
        var act = () => service.ShowBalloonTip("Title", "Message", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        act.Should().NotThrow();
    }

    [Fact]
    public void DI_CompositionRoot_RegistersAppSettingsSingleton()
    {
        using var appData = new IsolatedAppData();
        var provider = ServiceConfiguration.ConfigureServices(appData.Paths);

        var s1 = provider.GetRequiredService<AppSettings>();
        var s2 = provider.GetRequiredService<AppSettings>();
        var repo = provider.GetRequiredService<SettingsRepository>();

        s1.Should().NotBeNull();
        s1.Should().BeSameAs(s2, "AppSettings must be registered as a typed singleton");
        s1.Should().BeSameAs(repo.CurrentSettings, "SettingsRepository and DI must share the exact same instance");

        var tray = provider.GetRequiredService<ITrayIconService>();
        var toast = provider.GetRequiredService<IToastNotificationService>();
        tray.Should().NotBeNull();
        toast.Should().NotBeNull();

        (provider as IDisposable)?.Dispose();
    }

    [Fact]
    public void DI_CompositionRoot_WithPreconfiguredSettings_SuppliesSameInstanceToServices()
    {
        using var appData = new IsolatedAppData();
        var customSettings = new AppSettings
        {
            ToastNotificationsEnabled = false,
            QuietMode = true,
            SystemTrayEnabled = false,
            ScanBatchSize = 42
        };

        var provider = ServiceConfiguration.ConfigureServices(appData.Paths, customSettings);

        var resolved = provider.GetRequiredService<AppSettings>();
        resolved.Should().BeSameAs(customSettings);
        resolved.ScanBatchSize.Should().Be(42);

        var toast = provider.GetRequiredService<IToastNotificationService>();
        toast.AreNotificationsEnabled.Should().BeFalse();
        toast.IsQuietMode.Should().BeTrue();

        (provider as IDisposable)?.Dispose();
    }
}
