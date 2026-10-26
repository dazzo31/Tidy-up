using System.Drawing;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Data;
using TidyUp.Data.Repositories;
using TidyUp.Models;
using TidyUp.Services;
using TidyUp.ViewModels;
using TidyUp.Views;

namespace TidyUp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private TaskbarIcon? _trayIcon;
    private ITrayIconService? _trayIconService;
    private IToastNotificationService? _toastService;
    private MainWindow? _mainWindow;
    private bool _isExiting;
    public static AppSettings? AppSettings { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Configure services
            _serviceProvider = ServiceConfiguration.ConfigureServices();

            // Ensure database is created and up to date
            try
            {
                var dbStartup = await ServiceConfiguration.InitializeDatabaseAsync(_serviceProvider);
                if (!dbStartup.Success)
                {
                    var restored = dbStartup.RestoredFromBackup
                        ? $"\n\nYour previous database was restored from:\n{dbStartup.BackupPathUsed}"
                        : string.Empty;
                    MessageBox.Show(
                        $"TidyUp could not upgrade its database.{restored}\n\n{dbStartup.ErrorMessage}",
                        "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                if (dbStartup.WasCorruptedInitially && dbStartup.RestoredFromBackup)
                {
                    MessageBox.Show(
                        $"TidyUp's database was damaged and has been restored from:\n{dbStartup.BackupPathUsed}\n\nChanges made after that backup may be missing.",
                        "Database Restored", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
                var settingsRepo = _serviceProvider.GetRequiredService<TidyUp.Data.SettingsRepository>();
                if (settingsRepo.LastLoadResult?.IsCorrupt == true)
                {
                    if (settingsRepo.LastLoadResult.RecoveredFromBackup)
                    {
                        MessageBox.Show(
                            $"TidyUp's settings file was corrupted and has been restored from backup:\n{settingsRepo.LastLoadResult.BackupPathUsed}\n\nA copy of the corrupted file was preserved at:\n{settingsRepo.LastLoadResult.CorruptBackupPath}",
                            "Settings Restored From Backup", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"TidyUp's settings file was corrupted and could not be restored from backup. Default settings have been loaded.\n\nA copy of the corrupted file was preserved at:\n{settingsRepo.LastLoadResult.CorruptBackupPath}",
                            "Settings Corrupted", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load settings: {ex.Message}\nUsing defaults.",
                    "Settings Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                AppSettings = AppSettings.CreateDefault();
                _serviceProvider.GetRequiredService<TidyUp.Data.SettingsRepository>().SetShared(AppSettings);
            }

            // Apply the saved theme (and follow later changes) before any window is created
            try
            {
                _serviceProvider.GetRequiredService<TidyUp.Services.Theming.IThemeService>().Attach(AppSettings);
            }
            catch
            {
                // A theming failure must never stop the app from starting; it keeps the default light theme.
            }

            // First run: show the setup wizard once, before any main window exists
            if (AppSettings.ShowFirstRunWizard)
            {
                try
                {
                    await RunSetupWizardAsync();
                }
                catch (Exception ex)
                {
                    // The wizard is optional; a failure in it must never keep the app from starting.
                    Console.WriteLine($"Setup wizard failed: {ex.Message}");
                }
            }

            // Initialize tray icon and notification service
            _trayIconService = _serviceProvider.GetRequiredService<ITrayIconService>();
            _toastService = _serviceProvider.GetRequiredService<IToastNotificationService>();

            if (AppSettings?.SystemTrayEnabled == true)
            {
                try
                {
                    _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
                    if (_trayIcon != null)
                    {
                        _trayIconService.Initialize(_trayIcon);
                        if (_toastService is ToastNotificationService concreteToast)
                        {
                            concreteToast.SetTrayIconService(_trayIconService);
                        }
                    }
                }
                catch
                {
                    // Tray icon initialization fallback
                }
            }

            // Show main window
            _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = _mainWindow;

            if (_mainWindow.DataContext is MainWindowViewModel mainViewModel)
            {
                mainViewModel.SettingsViewModel.RunSetupWizardHandler = RunSetupWizardAsync;
            }
            
            // Handle window close to minimize to tray
            _mainWindow.Closing += MainWindow_Closing;
            
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unhandled Startup Exception: {ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Shows the first-run wizard modally and records that it has been seen.</summary>
    private async Task RunSetupWizardAsync()
    {
        // At first run the wizard is the only window, so closing it would otherwise end the application.
        var previousShutdownMode = ShutdownMode;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var viewModel = new FirstRunViewModel(
                _serviceProvider!.GetRequiredService<SettingsRepository>(),
                _serviceProvider!.GetRequiredService<IRuleRepository>(),
                AppSettings!);

            new FirstRunWindow(viewModel).ShowDialog();
            await viewModel.EnsureResolvedAsync();
        }
        finally
        {
            ShutdownMode = previousShutdownMode;
        }
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
        if (AppSettings?.SystemTrayEnabled == true && AppSettings?.MinimizeToTray == true && !_isExiting)
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

    private async void PauseMonitoring_Click(object sender, RoutedEventArgs e)
    {
        if (_trayIconService != null)
        {
            await _trayIconService.PauseMonitoringAsync();
        }
        else
        {
            _trayIcon?.ShowBalloonTip("TidyUp", "Monitoring paused", BalloonIcon.Info);
        }
    }

    private async void ResumeMonitoring_Click(object sender, RoutedEventArgs e)
    {
        if (_trayIconService != null)
        {
            await _trayIconService.ResumeMonitoringAsync();
        }
        else
        {
            _trayIcon?.ShowBalloonTip("TidyUp", "Monitoring resumed", BalloonIcon.Info);
        }
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
        // Transient: persisting MinimizeToTray=false here would silently turn the user's setting off for good.
        _isExiting = true;
        Shutdown();
    }

    #endregion
}

