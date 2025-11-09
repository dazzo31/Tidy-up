using System.Drawing;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Models;
using TidyUp.ViewModels;

namespace TidyUp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    public static AppSettings? AppSettings { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure services
        _serviceProvider = ServiceConfiguration.ConfigureServices();

        // Ensure database is created
        try
        {
            await ServiceConfiguration.EnsureDatabaseCreatedAsync(_serviceProvider);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize database: {ex.Message}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Load settings
        try
        {
            AppSettings = await ServiceConfiguration.LoadSettingsAsync(_serviceProvider);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load settings: {ex.Message}\nUsing defaults.",
                "Settings Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            AppSettings = AppSettings.CreateDefault();
        }

        // Initialize tray icon
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        if (_trayIcon != null)
        {
            // Create a simple icon from system resources
            _trayIcon.Icon = SystemIcons.Application;
        }

        // Show main window
        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        
        // Handle window close to minimize to tray
        _mainWindow.Closing += MainWindow_Closing;
        
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnExit(e);
    }

    #region Tray Icon Event Handlers

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Check if we should minimize to tray instead of closing
        if (AppSettings?.MinimizeToTray == true)
        {
            e.Cancel = true;
            _mainWindow?.Hide();
            _trayIcon?.ShowBalloonTip("TidyUp", "Application minimized to system tray", BalloonIcon.Info);
        }
    }

    private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    private void TrayIcon_TrayRightMouseDown(object sender, RoutedEventArgs e)
    {
        // Context menu opens automatically
    }

    private void ShowMainWindow_Click(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    private void PauseMonitoring_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement pause monitoring logic
        // This would need to be wired to the MonitoringService
        _trayIcon?.ShowBalloonTip("TidyUp", "Monitoring paused", BalloonIcon.Info);
    }

    private void ResumeMonitoring_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement resume monitoring logic
        _trayIcon?.ShowBalloonTip("TidyUp", "Monitoring resumed", BalloonIcon.Info);
    }

    private void ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
        if (_mainWindow?.DataContext is MainWindowViewModel vm)
        {
            vm.ViewLogsCommand.Execute(null);
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
        if (_mainWindow?.DataContext is MainWindowViewModel vm)
        {
            vm.OpenSettingsCommand.Execute(null);
        }
    }

    private void ExitApplication_Click(object sender, RoutedEventArgs e)
    {
        // Set flag to allow real exit
        if (AppSettings != null)
        {
            AppSettings.MinimizeToTray = false;
        }
        Shutdown();
    }

    #endregion
}

