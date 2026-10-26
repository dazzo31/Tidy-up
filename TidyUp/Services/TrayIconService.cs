using System.Drawing;
using Hardcodet.Wpf.TaskbarNotification;
using TidyUp.Models;
using TidyUp.Services.Monitoring;
using TidyUp.Services.State;

namespace TidyUp.Services;

/// <summary>
/// Service managing the system tray taskbar icon, context actions, and status updates.
/// </summary>
public class TrayIconService : ITrayIconService
{
    private readonly IFileMonitorService? _fileMonitorService;
    private readonly IWatcherHealthMonitor? _healthMonitor;
    private readonly AppSettings? _settings;
    private readonly IApplicationStateManager? _stateManager;
    private TaskbarIcon? _trayIcon;
    private TrayIconStatus _currentStatus = TrayIconStatus.Normal;
    private bool _disposed;

    public TrayIconStatus CurrentStatus => _currentStatus;

    public TrayIconService(
        IFileMonitorService? fileMonitorService = null,
        IWatcherHealthMonitor? healthMonitor = null,
        AppSettings? settings = null,
        IApplicationStateManager? stateManager = null)
    {
        _fileMonitorService = fileMonitorService;
        _healthMonitor = healthMonitor;
        _settings = settings;
        _stateManager = stateManager;

        if (_healthMonitor != null)
        {
            _healthMonitor.FolderHealthChanged += OnFolderHealthChanged;
        }

        if (_stateManager != null)
        {
            _stateManager.StateChanged += OnStateChanged;
        }

        if (_settings != null)
        {
            _settings.PropertyChanged += OnSettingsPropertyChanged;
        }
    }

    public void Initialize(TaskbarIcon trayIcon)
    {
        _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));
        if (_settings?.SystemTrayEnabled == false)
        {
            _trayIcon.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }
        _trayIcon.Visibility = System.Windows.Visibility.Visible;
        UpdateStatus(TrayIconStatus.Normal, "TidyUp - File Organization Manager");
    }

    public void UpdateStatus(TrayIconStatus status, string? tooltipText = null)
    {
        _currentStatus = status;

        if (_trayIcon == null || _settings?.SystemTrayEnabled == false)
            return;

        try
        {
            _trayIcon.Icon = CreateStatusIcon(status);
        }
        catch
        {
            // Fall back to application icon if drawing handle fails
            _trayIcon.Icon = SystemIcons.Application;
        }

        if (!string.IsNullOrEmpty(tooltipText))
        {
            _trayIcon.ToolTipText = tooltipText;
        }
    }

    public void ShowBalloonTip(string title, string message, BalloonIcon icon)
    {
        if (_settings?.SystemTrayEnabled == false)
            return;

        _trayIcon?.ShowBalloonTip(title, message, icon);
    }

    public Task PauseMonitoringAsync()
    {
        _fileMonitorService?.Pause();
        _stateManager?.TryTransitionTo(ApplicationLifecycleState.MonitoringPaused, "Paused from system tray");
        UpdateStatus(TrayIconStatus.Paused, "TidyUp - Monitoring Paused");
        ShowBalloonTip("TidyUp", "Monitoring paused", BalloonIcon.Info);
        return Task.CompletedTask;
    }

    public async Task ResumeMonitoringAsync()
    {
        if (_fileMonitorService != null)
        {
            await _fileMonitorService.ResumeAsync();
        }
        _stateManager?.TryTransitionTo(ApplicationLifecycleState.MonitoringRunning, "Resumed from system tray");
        UpdateStatus(TrayIconStatus.Normal, "TidyUp - Monitoring Active");
        ShowBalloonTip("TidyUp", "Monitoring resumed", BalloonIcon.Info);
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        if (e.NewState == ApplicationLifecycleState.MonitoringPaused)
        {
            UpdateStatus(TrayIconStatus.Paused, "TidyUp - Monitoring Paused");
        }
        else if (e.NewState == ApplicationLifecycleState.MonitoringRunning)
        {
            UpdateStatus(TrayIconStatus.Normal, "TidyUp - Monitoring Active");
        }
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.SystemTrayEnabled))
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visibility = _settings?.SystemTrayEnabled == true
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
        }
    }

    private void OnFolderHealthChanged(object? sender, FolderHealthReport e)
    {
        if (e.Status == WatcherHealthStatus.Degraded ||
            e.Status == WatcherHealthStatus.Disconnected ||
            e.Status == WatcherHealthStatus.Error)
        {
            UpdateStatus(TrayIconStatus.Warning, $"TidyUp - Warning: {e.Path} ({e.Status})");
        }
        else if (_fileMonitorService?.IsPaused == true)
        {
            UpdateStatus(TrayIconStatus.Paused, "TidyUp - Monitoring Paused");
        }
        else
        {
            UpdateStatus(TrayIconStatus.Normal, "TidyUp - File Organization Manager");
        }
    }

    /// <summary>
    /// Generates a status icon badge: Normal (Green), Paused (Gray), Warning (Amber).
    /// </summary>
    public static Icon CreateStatusIcon(TrayIconStatus status)
    {
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var brushColor = status switch
        {
            TrayIconStatus.Paused => Color.FromArgb(120, 120, 120),  // Gray
            TrayIconStatus.Warning => Color.FromArgb(230, 120, 0),   // Amber
            _ => Color.FromArgb(46, 125, 50)                         // Green / Active
        };

        using var brush = new SolidBrush(brushColor);
        g.FillEllipse(brush, 1, 1, 14, 14);

        using var borderPen = new Pen(Color.White, 1.5f);
        g.DrawEllipse(borderPen, 1, 1, 14, 14);

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_healthMonitor != null)
        {
            _healthMonitor.FolderHealthChanged -= OnFolderHealthChanged;
        }

        if (_stateManager != null)
        {
            _stateManager.StateChanged -= OnStateChanged;
        }

        if (_settings != null)
        {
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
        }

        GC.SuppressFinalize(this);
    }
}
