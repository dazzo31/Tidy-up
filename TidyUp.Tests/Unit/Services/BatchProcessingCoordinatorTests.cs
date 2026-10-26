using FluentAssertions;
using System.IO;
using Moq;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Processing;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Tests.Helpers;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class BatchProcessingCoordinatorTests : IDisposable
{
    private readonly TestFileHelper _fileHelper;
    private readonly VariableEngine _variableEngine;
    private readonly ActionExecutor _actionExecutor;
    private readonly BatchProcessingCoordinator _coordinator;

    public BatchProcessingCoordinatorTests()
    {
        _fileHelper = new TestFileHelper();
        _variableEngine = new VariableEngine();
        _actionExecutor = new ActionExecutor(
            _variableEngine,
            fileLockDetector: new FileLockDetector(),
            rollbackEngine: null);
        _coordinator = new BatchProcessingCoordinator(_actionExecutor);
    }

    public void Dispose()
    {
        _fileHelper.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ExecutionPlanGenerator_PopulatesPlannedCounterAndTargetFileName()
    {
        // Arrange
        var sourceDir = _fileHelper.CreateTestDirectory("epg_source");
        var destDir = _fileHelper.CreateTestDirectory("epg_dest");

        var file1 = _fileHelper.CreateTestFile(Path.Combine(sourceDir.Name, "alpha.txt"), "A");
        var file2 = _fileHelper.CreateTestFile(Path.Combine(sourceDir.Name, "beta.txt"), "B");

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Counter Rename Rule",
            IsEnabled = true,
            Actions =
            [
                new MoveFileAction
                {
                    Order = 1,
                    DestinationPath = destDir.FullName
                }
            ]
        };

        var generator = new ExecutionPlanGenerator(new RuleEngine(), _variableEngine);

        // Act
        var plan = await generator.GeneratePlanForFolderAsync(
            rule,
            sourceDir.FullName,
            includeSubfolders: false);

        // Assert
        plan.PlannedActions.Should().HaveCount(2);
        plan.PlannedActions[0].PlannedCounter.Should().Be(1);
        plan.PlannedActions[0].TargetFileName.Should().Be("alpha.txt");
        plan.PlannedActions[0].TargetPath.Should().Be(Path.Combine(destDir.FullName, "alpha.txt"));

        plan.PlannedActions[1].PlannedCounter.Should().Be(2);
        plan.PlannedActions[1].TargetFileName.Should().Be("beta.txt");
        plan.PlannedActions[1].TargetPath.Should().Be(Path.Combine(destDir.FullName, "beta.txt"));
    }

    [Fact]
    public async Task ExecuteBatchAsync_PartialSelection_ExecutesExactPlannedTargetPathAndFrozenCounter()
    {
        // Arrange
        var sourceDir = _fileHelper.CreateTestDirectory("partial_source");
        var destDir = _fileHelper.CreateTestDirectory("partial_dest");

        var files = new List<FileInfo>();
        for (int i = 1; i <= 5; i++)
        {
            files.Add(_fileHelper.CreateTestFile(Path.Combine(sourceDir.Name, $"file_{i}.txt"), $"Content {i}"));
        }

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Counter Move Rule",
            IsEnabled = true,
            Actions =
            [
                new RenameFileAction
                {
                    Order = 1,
                    NamePattern = "doc_{counter}"
                }
            ]
        };

        var generator = new ExecutionPlanGenerator(new RuleEngine(), _variableEngine);
        var plan = await generator.GeneratePlanForFolderAsync(rule, sourceDir.FullName, includeSubfolders: false);

        plan.PlannedActions.Should().HaveCount(5);
        // Verify preview targets: doc_001.txt, doc_002.txt, doc_003.txt, doc_004.txt, doc_005.txt
        plan.PlannedActions[4].TargetPath.Should().Contain("doc_005.txt");
        plan.PlannedActions[4].PlannedCounter.Should().Be(5);

        // Act: User selects ONLY file #5 (index 4) for batch execution
        var selectedAction = plan.PlannedActions[4];
        var batchResult = await _coordinator.ExecuteBatchAsync(
            [selectedAction],
            Guid.NewGuid());

        // Assert: The executed output on disk must be doc_005.txt, NOT doc_001.txt!
        batchResult.SucceededCount.Should().Be(1);
        batchResult.FailedCount.Should().Be(0);

        var expectedFinalPath = Path.Combine(sourceDir.FullName, "doc_005.txt");
        var driftedPath = Path.Combine(sourceDir.FullName, "doc_001.txt");

        File.Exists(expectedFinalPath).Should().BeTrue("file #5 must preserve its frozen counter 5 approved in preview");
        File.Exists(driftedPath).Should().BeFalse("file #5 must NOT be re-indexed to counter 1 during partial batch execution");
    }

    [Fact]
    public async Task ExecuteBatchAsync_InvokesExecutePlannedActionAsyncDirectly()
    {
        // Arrange
        var mockExecutor = new Mock<IActionExecutor>();
        var coordinator = new BatchProcessingCoordinator(mockExecutor.Object);
        var plannedAction = new PlannedFileAction
        {
            SourcePath = "C:\\test\\source.txt",
            TargetPath = "C:\\test\\target.txt",
            ActionType = ActionType.Move,
            FileAction = new MoveFileAction(),
            PlannedCounter = 42
        };

        var batchId = Guid.NewGuid();

        mockExecutor
            .Setup(x => x.ExecutePlannedActionAsync(plannedAction, batchId))
            .ReturnsAsync(new ActionResult
            {
                Success = true,
                ResultPath = "C:\\test\\target.txt",
                Type = ActionResultType.Success
            });

        // Act
        var result = await coordinator.ExecuteBatchAsync([plannedAction], batchId);

        // Assert
        result.SucceededCount.Should().Be(1);
        mockExecutor.Verify(x => x.ExecutePlannedActionAsync(plannedAction, batchId), Times.Once);
    }
}
