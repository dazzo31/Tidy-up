using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Models;

namespace TidyUp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
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

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnExit(e);
    }
}

