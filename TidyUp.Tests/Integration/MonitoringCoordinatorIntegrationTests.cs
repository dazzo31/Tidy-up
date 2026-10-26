using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Data;
using TidyUp.Data.Repositories;
using TidyUp.Models;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Monitoring;
using TidyUp.Services.Processing;
using TidyUp.Services.Rollback;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Services.State;
using Xunit;

namespace TidyUp.Tests.Integration;

public class MonitoringCoordinatorIntegrationTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly string _dbPath;
    private readonly string _incomingDir;
    private readonly string _destDir;

    public MonitoringCoordinatorIntegrationTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "TidyUp_Coord_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);

        _dbPath = Path.Combine(_testTempDir, "test_tidyup.db");
        _incomingDir = Path.Combine(_testTempDir, "Incoming");
        _destDir = Path.Combine(_testTempDir, "Destination");

        Directory.CreateDirectory(_incomingDir);
        Directory.CreateDirectory(_destDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task MonitoringCoordinator_StartsOnActivation_AndProcessesMatchingFilesThroughSafePipeline()
    {
        // Arrange: Build services
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        var dbContext = new TidyUpDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var ruleRepo = new RuleRepository(dbContext);
        var appSettings = AppSettings.CreateDefault();

        services.AddSingleton(dbContext);
        services.AddSingleton<IRuleRepository>(ruleRepo);
        services.AddSingleton(appSettings);
        services.AddSingleton<ISafeFileSystem, WindowsShellFileOperations>();
        services.AddSingleton<IFileLockDetector, FileLockDetector>();
        services.AddSingleton<IVariableEngine, VariableEngine>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IRollbackEngine>(sp => new RollbackEngine(directContext: sp.GetRequiredService<TidyUpDbContext>(), safeFileSystem: sp.GetRequiredService<ISafeFileSystem>()));
        services.AddSingleton<IActionExecutor, ActionExecutor>();
        services.AddSingleton<IFileMonitorService, FileMonitorService>();
        services.AddSingleton<IExecutionPlanGenerator, ExecutionPlanGenerator>();
        services.AddSingleton<IApplicationStateManager, ApplicationStateManager>();
        services.AddSingleton<IBatchProcessingCoordinator, BatchProcessingCoordinator>();
        services.AddSingleton<IMonitoringCoordinator, MonitoringCoordinator>();

        var sp = services.BuildServiceProvider();

        // Seed an enabled rule
        var rule = new Rule
        {
            Name = "Auto Move Text Files",
            IsEnabled = true,
            ExecutionOrder = 0,
            MonitoredFolders =
            {
                new MonitoredFolder
                {
                    Path = _incomingDir,
                    IncludeSubfolders = false
                }
            },
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = ".txt"
                    }
                }
            },
            Actions =
            {
                new MoveFileAction
                {
                    DestinationPath = _destDir,
                    Order = 0
                }
            }
        };

        await ruleRepo.AddAsync(rule);

        var coordinator = sp.GetRequiredService<IMonitoringCoordinator>();

        // Act 1: Start coordinator
        await coordinator.StartAsync();
        coordinator.IsRunning.Should().BeTrue("Monitoring coordinator should be running upon activation");

        // Act 2: Simulate file dropped into monitored folder
        var testFilePath = Path.Combine(_incomingDir, "report.txt");
        File.WriteAllText(testFilePath, "Sample text report content");

        // Process file via coordinator
        await coordinator.ProcessFileDetectedAsync(testFilePath, _incomingDir);

        // Assert: File was safely moved to destination through the batch execution pipeline
        var expectedDestFile = Path.Combine(_destDir, "report.txt");
        File.Exists(expectedDestFile).Should().BeTrue("File should have been moved to destination folder");
        File.Exists(testFilePath).Should().BeFalse("Source file should have been moved away from incoming folder");

        // Verify journal entry was written to database
        var journalEntries = await dbContext.OperationJournal.ToListAsync();
        journalEntries.Should().Contain(e => e.RuleName == "Auto Move Text Files" && e.ActionType == "Move");

        // Act 3: Stop coordinator
        await coordinator.StopAsync();
        coordinator.IsRunning.Should().BeFalse("Coordinator should be stopped");
    }
}
