using Hardcodet.Wpf.TaskbarNotification;
using TidyUp.Models;

namespace TidyUp.Services;

/// <summary>
/// Service implementation for managing desktop toast notifications with quiet mode suppression.
/// </summary>
public class ToastNotificationService : IToastNotificationService
{
    private readonly AppSettings? _settings;
    private ITrayIconService? _trayIconService;
    private bool _isQuietMode;

    public bool IsQuietMode
    {
        get => _isQuietMode || (_settings?.QuietMode ?? false);
        set => _isQuietMode = value;
    }

    public bool AreNotificationsEnabled
    {
        get => _settings?.ToastNotificationsEnabled ?? true;
        set
        {
            if (_settings != null) _settings.ToastNotificationsEnabled = value;
        }
    }

    public event EventHandler<ToastNotificationEventArgs>? NotificationTriggered;

    public ToastNotificationService(
        AppSettings? settings = null,
        ITrayIconService? trayIconService = null)
    {
        _settings = settings;
        _trayIconService = trayIconService;
    }

    public void SetTrayIconService(ITrayIconService trayIconService)
    {
        _trayIconService = trayIconService;
    }

    public void ShowNotification(string title, string message, ToastNotificationType type = ToastNotificationType.Information)
    {
        if (IsQuietMode || !AreNotificationsEnabled)
            return;

        if (type == ToastNotificationType.Error && _settings?.ShowErrorNotifications == false)
            return;

        if (type == ToastNotificationType.Success && _settings?.ShowSuccessNotifications == false)
            return;

        var args = new ToastNotificationEventArgs
        {
            Title = title,
            Message = message,
            Type = type
        };

        NotificationTriggered?.Invoke(this, args);

        var balloonIcon = type switch
        {
            ToastNotificationType.Warning => BalloonIcon.Warning,
            ToastNotificationType.Error => BalloonIcon.Error,
            _ => BalloonIcon.Info
        };

        _trayIconService?.ShowBalloonTip(title, message, balloonIcon);
    }

    public void NotifyBatchCompletedWithErrors(string batchName, int errorCount, int totalCount)
    {
        ShowNotification(
            "Batch Completed with Errors",
            $"Batch '{batchName}' completed: {errorCount} of {totalCount} operations failed. Check history for details.",
            ToastNotificationType.Warning);
    }

    public void NotifyFolderDisconnected(string folderPath, string reason)
    {
        ShowNotification(
            "Watched Folder Disconnected",
            $"Monitored directory '{folderPath}' is inaccessible: {reason}",
            ToastNotificationType.Error);
    }
}

