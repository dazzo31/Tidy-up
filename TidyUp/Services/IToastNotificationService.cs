namespace TidyUp.Services;

/// <summary>
/// Classification types for application toast notifications.
/// </summary>
public enum ToastNotificationType
{
    Information,
    Warning,
    Error,
    Success
}

/// <summary>
/// Event arguments containing notification metadata.
/// </summary>
public class ToastNotificationEventArgs : EventArgs
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ToastNotificationType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service interface for dispatching desktop and toast notifications.
/// </summary>
public interface IToastNotificationService
{
    /// <summary>
    /// Indicates whether quiet mode is active (suppressing all notifications).
    /// </summary>
    bool IsQuietMode { get; set; }

    /// <summary>
    /// Indicates whether toast notifications are enabled in application settings.
    /// </summary>
    bool AreNotificationsEnabled { get; set; }

    /// <summary>
    /// Raised whenever a notification is triggered and not suppressed.
    /// </summary>
    event EventHandler<ToastNotificationEventArgs>? NotificationTriggered;

    /// <summary>
    /// Displays a generic toast notification.
    /// </summary>
    void ShowNotification(string title, string message, ToastNotificationType type = ToastNotificationType.Information);

    /// <summary>
    /// Displays a notification when a batch file run completes with execution errors.
    /// </summary>
    void NotifyBatchCompletedWithErrors(string batchName, int errorCount, int totalCount);

    /// <summary>
    /// Displays an urgent notification when a monitored directory becomes inaccessible or disconnected.
    /// </summary>
    void NotifyFolderDisconnected(string folderPath, string reason);
}

