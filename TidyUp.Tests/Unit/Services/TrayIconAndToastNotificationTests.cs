using Moq;
using TidyUp.Models;
using TidyUp.Services;
using TidyUp.Services.Monitoring;
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
        var service = new TrayIconService(fileMonitorService: mockMonitor.Object);

        // Act: Pause
        await service.PauseMonitoringAsync();

        // Assert
        Assert.Equal(TrayIconStatus.Paused, service.CurrentStatus);
        mockMonitor.Verify(m => m.Pause(), Times.Once);

        // Act: Resume
        await service.ResumeMonitoringAsync();

        // Assert
        Assert.Equal(TrayIconStatus.Normal, service.CurrentStatus);
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
}
