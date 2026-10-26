using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TidyUp.Configuration;
using TidyUp.Data;
using TidyUp.Data.Entities;
using TidyUp.Data.Migrations;
using TidyUp.Data.Recovery;
using TidyUp.Data.Repositories;
using TidyUp.Models;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Diagnostics;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Folders;
using TidyUp.Services.Lifecycle;
using TidyUp.Services.Monitoring;
using TidyUp.Services.Processing;
using TidyUp.Services.Rollback;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Services.State;
using TidyUp.Services.Theming;
using TidyUp.Services.Validation;
using TidyUp.Services.Watcher;
using TidyUp.Tests.TestInfrastructure;
using TidyUp.ViewModels;
using TidyUp.ViewModels.Dialogs;
using TidyUp.ViewModels.RuleEditor;
using Xunit;

namespace TidyUp.Tests.Integration;

public class UiWorkflowEndToEndIntegrationTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly TidyUpDbContext _dbContext;
    private readonly IRuleRepository _ruleRepository;
    private readonly IApplicationStateManager _stateManager;
    private readonly IWatcherHealthMonitor _healthMonitor;
    private readonly IRuleValidator _ruleValidator;
    private readonly IRuleSummaryGenerator _ruleSummaryGenerator;
    private readonly IExecutionPlanGenerator _executionPlanGenerator;
    private readonly IExplainableDecisionEngine _explainableEngine;

    public UiWorkflowEndToEndIntegrationTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), $"TidyUp_UiIntegration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempTestDir);

        var dbPath = Path.Combine(_tempTestDir, "integration.db");
        var options = new DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _dbContext = new TidyUpDbContext(options);
        _dbContext.Database.EnsureCreated();

        _ruleRepository = new RuleRepository(_dbContext);
        _stateManager = new ApplicationStateManager();
        _healthMonitor = new WatcherHealthMonitor();
        _ruleValidator = new RuleValidator();
        _ruleSummaryGenerator = new RuleSummaryGenerator();
        _executionPlanGenerator = new ExecutionPlanGenerator(new RuleEngine(), new VariableEngine());
        _explainableEngine = new ExplainableDecisionEngine();
    }

    public void Dispose()
    {
        try
        {
            _dbContext.Dispose();
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task EndToEnd_Navigation_And_RuleLifecycle_Workflow()
    {
        var services = new ServiceCollection();
        var dbPath = Path.Combine(_tempTestDir, "integration.db");

        services.AddSingleton(new TidyUp.Configuration.TidyUpPaths(_tempTestDir));
        services.AddDbContext<TidyUpDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddDbContextFactory<TidyUpDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

        services.AddTransient<IRuleRepository, RuleRepository>();
        services.AddTransient<IActionLogRepository, ActionLogRepository>();
        services.AddSingleton<SettingsRepository>();

        services.AddSingleton<ISafeFileSystem, WindowsShellFileOperations>();
        services.AddSingleton<IFileLockDetector, FileLockDetector>();
        services.AddSingleton<IRetryQueueManager, RetryQueueManager>();
        services.AddSingleton<IVariableEngine, VariableEngine>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IActionExecutor, ActionExecutor>();
        services.AddSingleton<IWatcherHealthMonitor, WatcherHealthMonitor>();
        services.AddSingleton<IFileMonitorService, FileMonitorService>();
        services.AddSingleton<IWatcherReconciler>(sp => (FileMonitorService)sp.GetRequiredService<IFileMonitorService>());
#pragma warning disable CS0618
        services.AddSingleton<IImportExportService, ImportExportService>();
#pragma warning restore CS0618
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
        services.AddSingleton<IExplainableDecisionEngine, ExplainableDecisionEngine>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<RuleWizardViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PreviewViewModel>(sp =>
        {
            var planGenerator = sp.GetRequiredService<IExecutionPlanGenerator>();
            var actionExecutor = sp.GetRequiredService<IActionExecutor>();
            var discrepancyAnalyzer = sp.GetService<IExecutionDiscrepancyAnalyzer>();
            var batchCoordinator = sp.GetService<IBatchProcessingCoordinator>();
            var explainableEngine = sp.GetService<IExplainableDecisionEngine>();
            return new PreviewViewModel(planGenerator, actionExecutor, discrepancyAnalyzer, batchCoordinator, explainableEngine)
            {
                EnablePreFlightValidation = false
            };
        });
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<FoldersViewModel>();

        var sp = services.BuildServiceProvider();

        // 1. Initialize MainWindowViewModel
        var mainVm = sp.GetRequiredService<MainWindowViewModel>();
        mainVm.ConfirmDiscardRuleCallback = (_, _) => true;

        mainVm.CurrentView.Should().Be(NavigationView.Dashboard);
        mainVm.IsDashboardActive.Should().BeTrue();

        // 2. Navigate through all 6 core destinations
        mainVm.NavigateToRuleEditorCommand.Execute(null);
        mainVm.CurrentView.Should().Be(NavigationView.RuleEditor);
        mainVm.IsRulesActive.Should().BeTrue();

        mainVm.NavigateToFoldersCommand.Execute(null);
        mainVm.CurrentView.Should().Be(NavigationView.Folders);
        mainVm.IsFoldersActive.Should().BeTrue();

        mainVm.NavigateToPreviewReviewCommand.Execute(null);
        mainVm.CurrentView.Should().Be(NavigationView.PreviewReview);
        mainVm.IsPreviewActive.Should().BeTrue();

        mainVm.ViewHistoryCommand.Execute(null);
        mainVm.CurrentView.Should().Be(NavigationView.History);
        mainVm.IsHistoryActive.Should().BeTrue();

        mainVm.OpenSettingsCommand.Execute(null);
        mainVm.CurrentView.Should().Be(NavigationView.Settings);
        mainVm.IsSettingsActive.Should().BeTrue();

        // 3. Switch back to Rule Editor and create a new rule via Wizard
        mainVm.NavigateToRuleEditorCommand.Execute(null);
        var sourceFolder = Path.Combine(_tempTestDir, "Downloads");
        var destFolder = Path.Combine(_tempTestDir, "PDFs");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(destFolder);

        // Put a test PDF file in source
        var pdfPath = Path.Combine(sourceFolder, "statement.pdf");
        File.WriteAllText(pdfPath, "PDF sample content");

        var rule = new Rule
        {
            Name = "Organize PDFs",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = sourceFolder, IncludeSubfolders = false } },
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = ".pdf"
                    }
                }
            },
            Actions =
            {
                new MoveFileAction { DestinationPath = destFolder, Order = 1 }
            }
        };

        await mainVm.SaveRuleDirectAsync(rule);
        await mainVm.LoadRulesCommand.ExecuteAsync(null);
        mainVm.Rules.Should().Contain(r => r.Name == "Organize PDFs");

        mainVm.SelectedRule = mainVm.Rules.First(r => r.Name == "Organize PDFs");

        // 4. Test Dual-Mode Lossless State Synchronization
        mainVm.SetEditorMode(RuleEditorMode.GuidedWorkflow);
        mainVm.IsGuidedModeActive.Should().BeTrue();
        mainVm.WizardViewModel.RuleName.Should().Be("Organize PDFs");

        mainVm.SetEditorMode(RuleEditorMode.AdvancedTabs);
        mainVm.IsAdvancedModeActive.Should().BeTrue();
        mainVm.ActionEditor.Actions.Should().HaveCount(1);

        // 5. Run Preview simulation
        mainVm.NavigateToPreviewReviewCommand.Execute(null);
        await mainVm.PreviewViewModel.SimulateRuleAsync(rule);
        mainVm.CurrentView.Should().Be(NavigationView.PreviewReview);
        mainVm.PreviewViewModel.ExecutionPlan.Should().NotBeNull();
        mainVm.PreviewViewModel.MoveCount.Should().Be(1);

        // 6. Test Explainability
        var explanation = _explainableEngine.ExplainMatch(rule, pdfPath);
        explanation.OverallMatch.Should().BeTrue();
        explanation.ConditionResults.Should().HaveCount(1);
        explanation.ConditionResults[0].Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CompositionRoot_ResolvesAllServicesAndViewModels_CleanlyFromServiceConfiguration()
    {
        using var appData = new IsolatedAppData();
        var provider = ServiceConfiguration.ConfigureServices(appData.Paths);

        // 1. Validate real startup lifecycle: database initialization & settings loading
        var dbInitResult = await ServiceConfiguration.InitializeDatabaseAsync(provider);
        dbInitResult.Success.Should().BeTrue("database initialization and migrations must succeed");

        var loadedSettings = await ServiceConfiguration.LoadSettingsAsync(provider);
        loadedSettings.Should().NotBeNull("settings repository must load default or existing configuration");

        // 2. Core infrastructure & data layer
        provider.GetRequiredService<TidyUpPaths>().Should().NotBeNull();
        provider.GetRequiredService<TidyUpDbContext>().Should().NotBeNull();
        provider.GetRequiredService<IDbContextFactory<TidyUpDbContext>>().Should().NotBeNull();
        provider.GetRequiredService<IDatabaseMigrator>().Should().NotBeNull();
        provider.GetRequiredService<IDbIntegrityChecker>().Should().NotBeNull();
        provider.GetRequiredService<IDatabaseBackupManager>().Should().NotBeNull();
        provider.GetRequiredService<SettingsRepository>().Should().NotBeNull();
        provider.GetRequiredService<AppSettings>().Should().NotBeNull();
        provider.GetRequiredService<IRuleRepository>().Should().NotBeNull();
        provider.GetRequiredService<IActionLogRepository>().Should().NotBeNull();

        // 3. Core domain & engine services
        provider.GetRequiredService<ISafeFileSystem>().Should().NotBeNull();
        provider.GetRequiredService<IFileLockDetector>().Should().NotBeNull();
        provider.GetRequiredService<IRetryQueueManager>().Should().NotBeNull();
        provider.GetRequiredService<IVariableEngine>().Should().NotBeNull();
        provider.GetRequiredService<IRuleEngine>().Should().NotBeNull();
        provider.GetRequiredService<IActionExecutor>().Should().NotBeNull();
        provider.GetRequiredService<IWatcherHealthMonitor>().Should().NotBeNull();
        provider.GetRequiredService<IFileMonitorService>().Should().NotBeNull();
        provider.GetRequiredService<IWatcherReconciler>().Should().NotBeNull();
#pragma warning disable CS0618
        provider.GetRequiredService<IImportExportService>().Should().NotBeNull();
#pragma warning restore CS0618
        provider.GetRequiredService<IExecutionPlanGenerator>().Should().NotBeNull();
        provider.GetRequiredService<IRollbackEngine>().Should().NotBeNull();
        provider.GetRequiredService<IRuleValidator>().Should().NotBeNull();
        provider.GetRequiredService<IRuleSummaryGenerator>().Should().NotBeNull();
        provider.GetRequiredService<IApplicationStateManager>().Should().NotBeNull();
        provider.GetRequiredService<IExecutionDiscrepancyAnalyzer>().Should().NotBeNull();
        provider.GetRequiredService<IOperationTelemetryService>().Should().NotBeNull();
        provider.GetRequiredService<IBatchProcessingCoordinator>().Should().NotBeNull();
        provider.GetRequiredService<ITrayIconService>().Should().NotBeNull();
        provider.GetRequiredService<IToastNotificationService>().Should().NotBeNull();
        provider.GetRequiredService<IMultiRuleConflictAnalyzer>().Should().NotBeNull();
        provider.GetRequiredService<IRuleRevisionManager>().Should().NotBeNull();
        provider.GetRequiredService<IRuleSerializationService>().Should().NotBeNull();
        provider.GetRequiredService<IExplainableDecisionEngine>().Should().NotBeNull();
        provider.GetRequiredService<IFolderPathNormalizer>().Should().NotBeNull();
        provider.GetRequiredService<IFolderValidator>().Should().NotBeNull();
        provider.GetRequiredService<IMonitoredFolderCatalog>().Should().NotBeNull();
        provider.GetRequiredService<IMonitoringCoordinator>().Should().NotBeNull();
        provider.GetRequiredService<IWindowCloseCoordinator>().Should().NotBeNull();
        provider.GetRequiredService<IThemeService>().Should().NotBeNull();

        // 4. ViewModels
        provider.GetRequiredService<MainWindowViewModel>().Should().NotBeNull();
        provider.GetRequiredService<DashboardViewModel>().Should().NotBeNull();
        provider.GetRequiredService<RuleWizardViewModel>().Should().NotBeNull();
        provider.GetRequiredService<LogsViewModel>().Should().NotBeNull();
        provider.GetRequiredService<SettingsViewModel>().Should().NotBeNull();
        provider.GetRequiredService<PreviewViewModel>().Should().NotBeNull();
        provider.GetRequiredService<SafeguardConfirmationViewModel>().Should().NotBeNull();
        provider.GetRequiredService<HistoryViewModel>().Should().NotBeNull();
        provider.GetRequiredService<RuleEvaluationInspectorViewModel>().Should().NotBeNull();
        provider.GetRequiredService<ExecutionSummaryViewModel>().Should().NotBeNull();
        provider.GetRequiredService<RuleConflictsViewModel>().Should().NotBeNull();
        provider.GetRequiredService<RuleRevisionDiffViewModel>().Should().NotBeNull();
        provider.GetRequiredService<FoldersViewModel>().Should().NotBeNull();
        provider.GetRequiredService<ActionEditorViewModel>().Should().NotBeNull();
    }

    [Fact]
    public async Task EndToEnd_FullLifecycle_SimulateRule_ApplyBatch_And_Rollback_OnDiskFixtures()
    {
        using var appData = new IsolatedAppData();
        var provider = ServiceConfiguration.ConfigureServices(appData.Paths);
        await ServiceConfiguration.InitializeDatabaseAsync(provider);
        await ServiceConfiguration.LoadSettingsAsync(provider);

        using var fixture = new TestDirectoryFixture();
        var incomingDir = fixture.SourceDirectory;
        var organizedDir = fixture.DestinationDirectory;

        // Create initial test files in source directory
        var doc1 = fixture.CreateFile("Source/report_2026.docx", "Annual Report Content");
        var doc2 = fixture.CreateFile("Source/summary_2026.docx", "Summary Executive Brief");
        var txtFile = fixture.CreateFile("Source/notes.txt", "Scratch notes to keep untouched");

        var ruleRepo = provider.GetRequiredService<IRuleRepository>();
        var rule = new Rule
        {
            Name = "Organize Word Documents",
            IsEnabled = true,
            ExecutionOrder = 1,
            MonitoredFolders = { new MonitoredFolder { Path = incomingDir, IncludeSubfolders = false } },
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = ".docx"
                    }
                }
            },
            Actions =
            {
                new MoveFileAction
                {
                    DestinationPath = organizedDir,
                    ConflictResolution = ConflictResolution.Overwrite,
                    Order = 1
                }
            }
        };

        await ruleRepo.AddAsync(rule);

        // Phase 1: Dry-Run Simulation
        var planGenerator = provider.GetRequiredService<IExecutionPlanGenerator>();
        var plan = await planGenerator.GeneratePlanForFolderAsync(rule, incomingDir, includeSubfolders: false);

        plan.RuleName.Should().Be("Organize Word Documents");
        plan.PlannedActions.Should().HaveCount(2);
        plan.PlannedActions.Select(p => Path.GetFileName(p.SourcePath)).Should().BeEquivalentTo(new[] { "report_2026.docx", "summary_2026.docx" });

        // Verify disk untouched during simulation
        File.Exists(doc1.FullName).Should().BeTrue("source file 1 must remain untouched during simulation");
        File.Exists(doc2.FullName).Should().BeTrue("source file 2 must remain untouched during simulation");
        File.Exists(txtFile.FullName).Should().BeTrue("txt file must remain untouched");
        Directory.GetFiles(organizedDir).Should().BeEmpty("destination folder must remain empty during simulation");

        // Phase 2: Batch Execution
        var batchCoordinator = provider.GetRequiredService<IBatchProcessingCoordinator>();
        var batchId = Guid.NewGuid();
        var batchResult = await batchCoordinator.ExecuteBatchAsync(
            plan.PlannedActions.ToList(),
            batchId,
            progress: null,
            CancellationToken.None);

        batchResult.Status.Should().Be(BatchExecutionStatus.Completed, "batch execution must succeed");
        batchResult.SucceededCount.Should().Be(2);

        // Verify file movement on disk
        File.Exists(doc1.FullName).Should().BeFalse("source file 1 must have been moved");
        File.Exists(doc2.FullName).Should().BeFalse("source file 2 must have been moved");
        File.Exists(txtFile.FullName).Should().BeTrue("non-matching file must remain untouched in source");

        var destDoc1 = Path.Combine(organizedDir, "report_2026.docx");
        var destDoc2 = Path.Combine(organizedDir, "summary_2026.docx");
        File.Exists(destDoc1).Should().BeTrue("dest file 1 must exist");
        File.Exists(destDoc2).Should().BeTrue("dest file 2 must exist");

        // Verify telemetry recorded
        var logRepo = provider.GetRequiredService<IActionLogRepository>();
        var logs = await logRepo.GetFilteredAsync();
        logs.Should().HaveCount(2, "two execution events must be recorded in durable action log");
        logs.All(l => l.Status == "Success").Should().BeTrue();

        // Verify rule stats updated in database
        using (var verifyScope = provider.CreateScope())
        {
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IRuleRepository>();
            var updatedRule = await verifyRepo.GetByIdAsync(rule.Id);
            updatedRule.Should().NotBeNull();
            updatedRule!.FilesProcessedCount.Should().Be(2);
            updatedRule.LastRunDate.Should().NotBeNull();
        }

        // Phase 3: Rollback
        var rollbackEngine = provider.GetRequiredService<IRollbackEngine>();
        var rollbackResult = await rollbackEngine.RollbackBatchAsync(batchId, CancellationToken.None);

        rollbackResult.OverallSuccess.Should().BeTrue("rollback of batch must succeed");
        rollbackResult.RolledBackCount.Should().Be(2);

        // Verify disk files restored to original locations
        File.Exists(doc1.FullName).Should().BeTrue("source file 1 must be restored");
        File.Exists(doc2.FullName).Should().BeTrue("source file 2 must be restored");
        File.Exists(destDoc1).Should().BeFalse("dest file 1 must be removed after rollback");
        File.Exists(destDoc2).Should().BeFalse("dest file 2 must be removed after rollback");
    }

    [Fact]
    public async Task EndToEnd_MonitoringCoordinator_BackgroundPipeline_AcceptanceScenario()
    {
        using var appData = new IsolatedAppData();
        var provider = ServiceConfiguration.ConfigureServices(appData.Paths);
        await ServiceConfiguration.InitializeDatabaseAsync(provider);
        await ServiceConfiguration.LoadSettingsAsync(provider);

        using var fixture = new TestDirectoryFixture();
        var watchDir = fixture.SourceDirectory;
        var targetDir = fixture.DestinationDirectory;

        var ruleRepo = provider.GetRequiredService<IRuleRepository>();
        var rule = new Rule
        {
            Name = "Auto-Move Invoices",
            IsEnabled = true,
            ExecutionOrder = 0,
            MonitoredFolders = { new MonitoredFolder { Path = watchDir, IncludeSubfolders = false } },
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = ".pdf"
                    }
                }
            },
            Actions =
            {
                new MoveFileAction
                {
                    DestinationPath = targetDir,
                    ConflictResolution = ConflictResolution.Overwrite,
                    Order = 1
                }
            }
        };

        await ruleRepo.AddAsync(rule);

        var coordinator = provider.GetRequiredService<IMonitoringCoordinator>();
        await coordinator.StartAsync();
        coordinator.IsRunning.Should().BeTrue("monitoring coordinator must be active after start");

        // Simulate incoming invoice file
        var invoicePath = Path.Combine(watchDir, "invoice_1001.pdf");
        File.WriteAllText(invoicePath, "Invoice test data 2026");

        // Process file detection event through the coordinator
        await coordinator.ProcessFileDetectedAsync(invoicePath, watchDir);

        // Verify the file was matched, planned, and moved safely
        File.Exists(invoicePath).Should().BeFalse("original invoice should have been processed and moved");
        var movedPath = Path.Combine(targetDir, "invoice_1001.pdf");
        File.Exists(movedPath).Should().BeTrue("invoice should now be in target directory");
        File.ReadAllText(movedPath).Should().Be("Invoice test data 2026");

        // Stop coordinator cleanly
        await coordinator.StopAsync();
        coordinator.IsRunning.Should().BeFalse("monitoring coordinator must stop cleanly");
    }
}

// Extension helper for testing
internal static class MainWindowViewModelTestExtensions
{
    public static async Task SaveRuleDirectAsync(this MainWindowViewModel vm, Rule rule)
    {
        var repo = (IRuleRepository)typeof(MainWindowViewModel)
            .GetField("_ruleRepository", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(vm)!;

        await repo.AddAsync(rule);
    }
}
