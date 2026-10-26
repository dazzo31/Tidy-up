using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Configuration;
using TidyUp.Data;
using TidyUp.Data.Migrations;
using TidyUp.Data.Recovery;
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
using TidyUp.Services.Folders;
using TidyUp.Services.Theming;
using TidyUp.ViewModels;
using TidyUp.ViewModels.RuleEditor;

namespace TidyUp;

/// <summary>
/// Configures dependency injection for the application.
/// </summary>
public static class ServiceConfiguration
{
    public static IServiceProvider ConfigureServices(TidyUpPaths? paths = null, AppSettings? initialSettings = null)
    {
        paths ??= TidyUpPaths.FromEnvironmentOrDefault();
        Directory.CreateDirectory(paths.AppDataRoot);

        var services = new ServiceCollection();
        services.AddSingleton(paths);

        // Database: use a factory so each scope gets a fresh context
        services.AddDbContextFactory<TidyUpDbContext>(options => options.UseSqlite(paths.ConnectionString));

        // Also register DbContext directly for simple transient use
        services.AddDbContext<TidyUpDbContext>(options => options.UseSqlite(paths.ConnectionString));

        services.AddSingleton<IDatabaseBackupManager>(sp => new DatabaseBackupManager(sp.GetRequiredService<TidyUpPaths>()));
        services.AddSingleton<IDbIntegrityChecker, DbIntegrityChecker>();
        services.AddSingleton<IDatabaseMigrator, DatabaseMigrator>();
        services.AddSingleton<IThemeService>(_ => new ThemeService(
            Application.Current.Resources,
            () => new ResourceDictionary { Source = new Uri("pack://application:,,,/TidyUp;component/Styles/Colors.Dark.xaml", UriKind.Absolute) },
            marshal: action => Application.Current.Dispatcher.BeginInvoke(action),
            watchSystemChanges: true));

        // Repositories � scoped so they share a DbContext within a logical operation
        services.AddTransient<IRuleRepository, RuleRepository>();
        services.AddTransient<IActionLogRepository, ActionLogRepository>();
        services.AddSingleton<SettingsRepository>(sp =>
        {
            var repo = new SettingsRepository(sp.GetRequiredService<TidyUpPaths>());
            if (initialSettings != null)
            {
                repo.SetShared(initialSettings);
            }
            return repo;
        });
        services.AddSingleton<AppSettings>(sp => sp.GetRequiredService<SettingsRepository>().CurrentSettings);

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
        services.AddSingleton<IOperationTelemetryService>(sp => new OperationTelemetryService(sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IBatchProcessingCoordinator>(sp => new BatchProcessingCoordinator(
            sp.GetRequiredService<IActionExecutor>(),
            sp.GetService<IApplicationStateManager>(),
            scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<ITrayIconService>(sp => new TrayIconService(
            sp.GetRequiredService<IFileMonitorService>(),
            sp.GetRequiredService<IWatcherHealthMonitor>(),
            sp.GetRequiredService<AppSettings>(),
            sp.GetRequiredService<IApplicationStateManager>()));
        services.AddSingleton<IToastNotificationService>(sp => new ToastNotificationService(
            sp.GetRequiredService<AppSettings>(),
            sp.GetRequiredService<ITrayIconService>()));
        services.AddSingleton<IMultiRuleConflictAnalyzer, MultiRuleConflictAnalyzer>();
        services.AddTransient<IRuleRevisionManager, RuleRevisionManager>();
        services.AddSingleton<IRuleSerializationService, RuleSerializationService>();
        services.AddSingleton<IExplainableDecisionEngine, ExplainableDecisionEngine>();
        services.AddSingleton<IFolderPathNormalizer, FolderPathNormalizer>();
        services.AddSingleton<IFolderValidator, FolderValidator>();
        services.AddSingleton<IMonitoredFolderCatalog, MonitoredFolderCatalog>();
        services.AddSingleton<TidyUp.Services.Monitoring.IMonitoringCoordinator, TidyUp.Services.Monitoring.MonitoringCoordinator>();
        services.AddSingleton<TidyUp.Services.Lifecycle.IWindowCloseCoordinator, TidyUp.Services.Lifecycle.WindowCloseCoordinator>();

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
        services.AddTransient<FoldersViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<Views.DashboardView>();
        services.AddTransient<Views.FoldersView>();
        services.AddTransient<Views.PreviewView>();
        services.AddTransient<Views.RuleEditor.RuleWizardView>();
        services.AddTransient<Views.PreviewWindow>();
        services.AddTransient<Views.Dialogs.SafeguardConfirmationDialog>();
        services.AddTransient<Views.Dialogs.RuleDiagnosticsDialog>();
        services.AddTransient<Views.ExecutionSummaryWindow>();
        services.AddTransient<Views.HistoryView>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Verifies, backs up (only when a schema change is pending), migrates, and rolls back on failure.
    /// </summary>
    public static async Task<DatabaseStartupResult> InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        var paths = serviceProvider.GetRequiredService<TidyUpPaths>();
        var migrator = serviceProvider.GetRequiredService<IDatabaseMigrator>();
        var integrityChecker = serviceProvider.GetRequiredService<IDbIntegrityChecker>();

        return await integrityChecker.SafeInitializeDatabaseAsync(
            paths.DatabasePath,
            () => migrator.MigrateAsync(),
            async () =>
            {
                try
                {
                    return await migrator.HasPendingMigrationsAsync();
                }
                catch
                {
                    // Unreadable database: keep a copy of it before anything else touches it.
                    return true;
                }
            });
    }

    /// <summary>
    /// Loads application settings and registers as singleton.
    /// </summary>
    public static async Task<AppSettings> LoadSettingsAsync(IServiceProvider serviceProvider)
    {
        var settingsRepo = serviceProvider.GetRequiredService<SettingsRepository>();
        var settings = await settingsRepo.LoadSharedAsync();
        return settings;
    }
}
