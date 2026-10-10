using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Data;
using TidyUp.Data.Repositories;
using TidyUp.Models;
using TidyUp.Services;
using TidyUp.Services.Diagnostics;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Monitoring;
using TidyUp.Services.Processing;
using TidyUp.Services.Rollback;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Services.Validation;
using TidyUp.Services.Watcher;
using TidyUp.Services.State;
using TidyUp.ViewModels;
using TidyUp.ViewModels.RuleEditor;

namespace TidyUp;

/// <summary>
/// Configures dependency injection for the application.
/// </summary>
public static class ServiceConfiguration
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Database � use a factory so each scope gets a fresh context
        services.AddDbContextFactory<TidyUpDbContext>(options =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TidyUp");
            Directory.CreateDirectory(appDataPath);

            var dbPath = Path.Combine(appDataPath, "tidyup.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Also register DbContext directly for simple transient use
        services.AddDbContext<TidyUpDbContext>(options =>
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TidyUp");
            Directory.CreateDirectory(appDataPath);

            var dbPath = Path.Combine(appDataPath, "tidyup.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Repositories � scoped so they share a DbContext within a logical operation
        services.AddTransient<IRuleRepository, RuleRepository>();
        services.AddTransient<IActionLogRepository, ActionLogRepository>();
        services.AddSingleton<SettingsRepository>();

        // Services � stateless services can be singletons
        services.AddSingleton<ISafeFileSystem, WindowsShellFileOperations>();
        services.AddSingleton<IFileLockDetector, FileLockDetector>();
        services.AddSingleton<IRetryQueueManager, RetryQueueManager>();
        services.AddSingleton<IVariableEngine, VariableEngine>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IActionExecutor, ActionExecutor>();
        services.AddSingleton<IWatcherHealthMonitor, WatcherHealthMonitor>();
        services.AddSingleton<IFileMonitorService, FileMonitorService>();
        services.AddSingleton<IWatcherReconciler>(sp => (FileMonitorService)sp.GetRequiredService<IFileMonitorService>());
        services.AddSingleton<IImportExportService, ImportExportService>();
        services.AddSingleton<IExecutionPlanGenerator, ExecutionPlanGenerator>();
        services.AddSingleton<IRollbackEngine, RollbackEngine>();
        services.AddSingleton<IRuleValidator, RuleValidator>();
        services.AddSingleton<IRuleSummaryGenerator, RuleSummaryGenerator>();
        services.AddSingleton<IApplicationStateManager, ApplicationStateManager>();
        services.AddSingleton<IExecutionDiscrepancyAnalyzer, ExecutionDiscrepancyAnalyzer>();
        services.AddSingleton<IBatchProcessingCoordinator, BatchProcessingCoordinator>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IToastNotificationService, ToastNotificationService>();
        services.AddSingleton<IMultiRuleConflictAnalyzer, MultiRuleConflictAnalyzer>();
        services.AddTransient<IRuleRevisionManager, RuleRevisionManager>();
        services.AddSingleton<IRuleSerializationService, RuleSerializationService>();

        // ViewModels  transient so each request gets a fresh instance
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<RuleWizardViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PreviewViewModel>();
        services.AddTransient<ViewModels.Dialogs.SafeguardConfirmationViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<RuleEvaluationInspectorViewModel>();
        services.AddTransient<ExecutionSummaryViewModel>();
        services.AddTransient<RuleConflictsViewModel>();
        services.AddTransient<RuleRevisionDiffViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<Views.DashboardView>();
        services.AddTransient<Views.RuleEditor.RuleWizardView>();
        services.AddTransient<Views.PreviewWindow>();
        services.AddTransient<Views.Dialogs.SafeguardConfirmationDialog>();
        services.AddTransient<Views.Dialogs.RuleDiagnosticsDialog>();
        services.AddTransient<Views.ExecutionSummaryWindow>();
        services.AddTransient<Views.HistoryView>();

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
