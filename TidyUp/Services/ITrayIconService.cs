using Hardcodet.Wpf.TaskbarNotification;

namespace TidyUp.Services;

/// <summary>
/// Status state displayed by the taskbar system tray icon.
/// </summary>
public enum TrayIconStatus
{
    /// <summary>
    /// Active, healthy background monitoring (Green / Normal icon).
    /// </summary>
    Normal,

    /// <summary>
    /// File monitoring paused by user (Gray icon).
    /// </summary>
    Paused,

    /// <summary>
    /// Degraded or disconnected monitored folder (Amber warning icon).
    /// </summary>
    Warning
}

/// <summary>
/// Service interface managing the system tray icon, context menu actions, and visual status indication.
/// </summary>
public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Current tray status level.
    /// </summary>
    TrayIconStatus CurrentStatus { get; }

    /// <summary>
    /// Binds the service to the UI TaskbarIcon control.
    /// </summary>
    void Initialize(TaskbarIcon trayIcon);

    /// <summary>
    /// Updates the tray icon appearance and tooltip based on system status.
    /// </summary>
    void UpdateStatus(TrayIconStatus status, string? tooltipText = null);

    /// <summary>
    /// Displays a standard balloon tip / system notification.
    /// </summary>
    void ShowBalloonTip(string title, string message, BalloonIcon icon);

    /// <summary>
    /// Pauses background file monitoring via the file monitor service.
    /// </summary>
    Task PauseMonitoringAsync();

    /// <summary>
    /// Resumes background file monitoring and flushes buffered events.
    /// </summary>
    Task ResumeMonitoringAsync();
}

