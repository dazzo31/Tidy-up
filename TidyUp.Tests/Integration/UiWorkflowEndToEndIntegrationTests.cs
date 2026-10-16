using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TidyUp.Data;
using TidyUp.Data.Entities;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Diagnostics;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Monitoring;
using TidyUp.Services.Processing;
using TidyUp.Services.Rollback;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Services.State;
using TidyUp.Services.Validation;
using TidyUp.Services.Watcher;
using TidyUp.ViewModels;
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
