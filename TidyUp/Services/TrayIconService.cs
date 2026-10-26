using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly IFileMonitorService? _fileMonitorService;
    private readonly IWatcherHealthMonitor? _healthMonitor;
    private readonly AppSettings? _settings;
    private readonly IApplicationStateManager? _stateManager;
    private readonly ConcurrentDictionary<string, WatcherHealthStatus> _folderHealthStates = new(StringComparer.OrdinalIgnoreCase);

    private TaskbarIcon? _trayIcon;
    private TrayIconStatus _currentStatus = TrayIconStatus.Normal;
    private Icon? _currentCreatedIcon;
    private IntPtr _currentHIcon = IntPtr.Zero;
    private bool _disposed;

    private static Icon? _cachedBaseIcon;
    private static readonly object _baseIconLock = new();

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
            var (icon, hIcon) = CreateStatusIconWithNativeHandle(status);
            CleanupCurrentIcon();
            _currentCreatedIcon = icon;
            _currentHIcon = hIcon;
            _trayIcon.Icon = icon;
        }
        catch
        {
            CleanupCurrentIcon();
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
        RecomputeAggregateStatus();
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
            RecomputeAggregateStatus();
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
        if (e == null || string.IsNullOrWhiteSpace(e.Path)) return;
        _folderHealthStates[e.Path] = e.Status;
        RecomputeAggregateStatus();
    }

    public void RecomputeAggregateStatus()
    {
        if (_fileMonitorService?.IsPaused == true || _stateManager?.CurrentState == ApplicationLifecycleState.MonitoringPaused)
        {
            UpdateStatus(TrayIconStatus.Paused, "TidyUp - Monitoring Paused");
            return;
        }

        var statuses = _folderHealthStates.Values.ToList();
        int disconnectedCount = statuses.Count(s => s == WatcherHealthStatus.Disconnected);
        int errorCount = statuses.Count(s => s == WatcherHealthStatus.Error);
        int degradedCount = statuses.Count(s => s == WatcherHealthStatus.Degraded);
        int pausedCount = statuses.Count(s => s == WatcherHealthStatus.Paused);

        if (disconnectedCount > 0 || errorCount > 0)
        {
            int totalAlerts = disconnectedCount + errorCount;
            UpdateStatus(TrayIconStatus.Warning, $"TidyUp - Alert: {totalAlerts} folder(s) inaccessible or error");
        }
        else if (degradedCount > 0)
        {
            UpdateStatus(TrayIconStatus.Warning, $"TidyUp - Warning: {degradedCount} folder(s) degraded");
        }
        else if (statuses.Count > 0 && pausedCount == statuses.Count)
        {
            UpdateStatus(TrayIconStatus.Paused, "TidyUp - All folders paused");
        }
        else
        {
            string tooltip = statuses.Count > 0
                ? $"TidyUp - Monitoring Active ({statuses.Count} folder(s))"
                : "TidyUp - File Organization Manager";
            UpdateStatus(TrayIconStatus.Normal, tooltip);
        }
    }

    private void CleanupCurrentIcon()
    {
        if (_currentCreatedIcon != null)
        {
            try
            {
                _currentCreatedIcon.Dispose();
            }
            catch { }
            _currentCreatedIcon = null;
        }

        if (_currentHIcon != IntPtr.Zero)
        {
            try
            {
                DestroyIcon(_currentHIcon);
            }
            catch { }
            _currentHIcon = IntPtr.Zero;
        }
    }

    private static Icon? LoadBaseBrandedIcon()
    {
        if (_cachedBaseIcon != null)
            return _cachedBaseIcon;

        lock (_baseIconLock)
        {
            if (_cachedBaseIcon != null)
                return _cachedBaseIcon;

            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "tidyup.ico");
                if (File.Exists(iconPath))
                {
                    _cachedBaseIcon = new Icon(iconPath, 16, 16);
                    return _cachedBaseIcon;
                }
            }
            catch { }

            try
            {
                _cachedBaseIcon = SystemIcons.Application;
                return _cachedBaseIcon;
            }
            catch { }

            return null;
        }
    }

    /// <summary>
    /// Generates a status icon badge: Normal (Green), Paused (Gray), Warning (Amber).
    /// </summary>
    public static Icon CreateStatusIcon(TrayIconStatus status)
    {
        var (icon, _) = CreateStatusIconWithNativeHandle(status);
        return icon;
    }

    /// <summary>
    /// Generates a status icon badge retaining the branded application base with a status dot overlay,
    /// returning both the managed Icon wrapper and the native HICON handle for lifecycle management.
    /// </summary>
    public static (Icon Icon, IntPtr HIcon) CreateStatusIconWithNativeHandle(TrayIconStatus status)
    {
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

        var baseIcon = LoadBaseBrandedIcon();
        if (baseIcon != null)
        {
            g.DrawIcon(baseIcon, new Rectangle(0, 0, 16, 16));
        }
        else
        {
            using var baseBrush = new SolidBrush(Color.FromArgb(24, 118, 209));
            g.FillRectangle(baseBrush, 1, 1, 14, 14);
        }

        var badgeColor = status switch
        {
            TrayIconStatus.Paused => Color.FromArgb(128, 128, 128),  // Gray
            TrayIconStatus.Warning => Color.FromArgb(230, 81, 0),    // Amber / Orange
            _ => Color.FromArgb(46, 125, 50)                         // Green
        };

        // Status badge dot at bottom-right corner
        using var borderPen = new Pen(Color.White, 1.2f);
        using var brush = new SolidBrush(badgeColor);
        g.FillEllipse(brush, 9, 9, 6, 6);
        g.DrawEllipse(borderPen, 9, 9, 6, 6);

        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        return (icon, hIcon);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        CleanupCurrentIcon();

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
