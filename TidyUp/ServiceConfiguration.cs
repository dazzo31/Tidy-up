using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Data;
using TidyUp.Data.Repositories;
using TidyUp.Models;
using TidyUp.Services;
using TidyUp.ViewModels;

namespace TidyUp;

/// <summary>
/// Configures dependency injection for the application.
/// </summary>
public static class ServiceConfiguration
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Database
        services.AddDbContext<TidyUpDbContext>(options =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TidyUp");
            Directory.CreateDirectory(appDataPath);

            var dbPath = Path.Combine(appDataPath, "tidyup.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Repositories
        services.AddTransient<IRuleRepository, RuleRepository>();
        services.AddTransient<IActionLogRepository, ActionLogRepository>();
        services.AddSingleton<SettingsRepository>();

        // Services
        services.AddSingleton<IVariableEngine, VariableEngine>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IActionExecutor, ActionExecutor>();
        services.AddSingleton<IFileMonitorService, FileMonitorService>();
        services.AddSingleton<IImportExportService, ImportExportService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<LogViewerViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<SettingsViewModel>(sp =>
        {
            var settingsRepo = sp.GetRequiredService<SettingsRepository>();
            // AppSettings will be loaded in App.xaml.cs startup
            var settings = App.AppSettings ?? AppSettings.CreateDefault();
            return new SettingsViewModel(settingsRepo, settings);
        });
        services.AddTransient<RulePreviewViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<Views.LogViewerWindow>();
        services.AddTransient<Views.SettingsWindow>();
        services.AddTransient<Views.RulePreviewWindow>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Ensures the database is created and migrated.
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TidyUpDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Loads application settings and registers as singleton.
    /// </summary>
    public static async Task<AppSettings> LoadSettingsAsync(IServiceProvider serviceProvider)
    {
        var settingsRepo = serviceProvider.GetRequiredService<SettingsRepository>();
        var settings = await settingsRepo.LoadAsync();
        return settings;
    }
}
