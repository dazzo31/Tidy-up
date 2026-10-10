using System.IO;
using Moq;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Processing;
using TidyUp.Services.Rollback;
using TidyUp.Services.State;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class BatchCancellationAndPauseTests : IDisposable
{
    private readonly string _testDirectory;

    public BatchCancellationAndPauseTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "TidyUp_BatchCancelTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }

    [Fact]
    public async Task BatchCoordinator_WhenCancelledAtFile15_CompletesFile15AndLeavesRemainingUntouched()
    {
        // Arrange
        var mockExecutor = new Mock<IActionExecutor>();
        var coordinator = new BatchProcessingCoordinator(mockExecutor.Object);
        var cts = new CancellationTokenSource();

        var plannedActions = new List<PlannedFileAction>();
        for (int i = 1; i <= 100; i++)
        {
            var testFilePath = Path.Combine(_testDirectory, $"file_{i}.txt");
            File.WriteAllText(testFilePath, $"content {i}");

            plannedActions.Add(new PlannedFileAction
            {
                SourcePath = testFilePath,
                ActionType = ActionType.Move,
                FileAction = new MoveFileAction { DestinationPath = Path.Combine(_testDirectory, "dest") }
            });
        }

        int executedCount = 0;
        mockExecutor
            .Setup(e => e.ExecuteActionAsync(It.IsAny<FileAction>(), It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()))
            .Returns<FileAction, FileInfo, int?, Guid?>(async (act, fi, cnt, bid) =>
            {
                executedCount++;
                // Simulate file 15 triggering cancellation during its execution
                if (executedCount == 15)
                {
                    cts.Cancel();
                }

                // Simulate brief execution work that must complete atomically
                await Task.Yield();

                return new ActionResult
                {
                    Success = true,
                    ResultPath = Path.Combine(_testDirectory, "dest", fi.Name),
                    Type = ActionResultType.Success
                };
            });

        // Act
        var result = await coordinator.ExecuteBatchAsync(
            plannedActions,
            Guid.NewGuid(),
            progress: null,
            cancellationToken: cts.Token);

        // Assert: Exactly 15 items were executed, 85 were cancelled/untouched
        Assert.Equal(BatchExecutionStatus.PartiallyCompleted, result.Status);
        Assert.True(result.WasCancelled);
        Assert.Equal(15, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(85, result.CancelledOrSkippedCount);
        Assert.Equal(100, result.TotalItems);

        // Files 1 to 15 completed
        for (int i = 0; i < 15; i++)
        {
            Assert.False(result.ItemResults[i].Skipped);
            Assert.True(result.ItemResults[i].Success);
        }

        // Files 16 to 100 were skipped without scheduling
        for (int i = 15; i < 100; i++)
        {
            Assert.True(result.ItemResults[i].Skipped);
            Assert.False(result.ItemResults[i].Success);
        }

        // Executor was invoked exactly 15 times
        mockExecutor.Verify(
            e => e.ExecuteActionAsync(It.IsAny<FileAction>(), It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()),
            Times.Exactly(15));
    }

    [Fact]
    public async Task BatchCoordinator_WhenEmpty_ReturnsCompletedWithZeroItems()
    {
        // Arrange
        var mockExecutor = new Mock<IActionExecutor>();
        var coordinator = new BatchProcessingCoordinator(mockExecutor.Object);

        // Act
        var result = await coordinator.ExecuteBatchAsync([], Guid.NewGuid());

        // Assert
        Assert.Equal(BatchExecutionStatus.Completed, result.Status);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.SucceededCount);
        Assert.False(result.WasCancelled);
    }

    [Fact]
    public async Task BatchCoordinator_WhenOperationFails_ContinuesAndCountsFailure()
    {
        // Arrange
        var mockExecutor = new Mock<IActionExecutor>();
        var coordinator = new BatchProcessingCoordinator(mockExecutor.Object);

        var file1 = Path.Combine(_testDirectory, "f1.txt");
        var file2 = Path.Combine(_testDirectory, "f2.txt");
        File.WriteAllText(file1, "1");
        File.WriteAllText(file2, "2");

        var actions = new List<PlannedFileAction>
        {
            new() { SourcePath = file1, FileAction = new MoveFileAction() },
            new() { SourcePath = file2, FileAction = new MoveFileAction() }
        };

        mockExecutor
            .Setup(e => e.ExecuteActionAsync(It.IsAny<FileAction>(), It.Is<FileInfo>(f => f.FullName == file1), It.IsAny<int?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new ActionResult { Success = false, ErrorMessage = "Locked by process" });

        mockExecutor
            .Setup(e => e.ExecuteActionAsync(It.IsAny<FileAction>(), It.Is<FileInfo>(f => f.FullName == file2), It.IsAny<int?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new ActionResult { Success = true });

        // Act
        var result = await coordinator.ExecuteBatchAsync(actions, Guid.NewGuid());

        // Assert
        Assert.Equal(BatchExecutionStatus.Completed, result.Status);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(2, result.TotalItems);
    }

    [Fact]
    public async Task FileMonitorService_WhenPaused_BuffersIncomingEventsWithoutDropping()
    {
        // Arrange
        var mockLockDetector = new Mock<IFileLockDetector>();
        mockLockDetector.Setup(d => d.IsTemporaryOrIncompleteFile(It.IsAny<string>())).Returns(false);
        mockLockDetector.Setup(d => d.IsFileReady(It.IsAny<string>())).Returns(true);

        using var monitor = new FileMonitorService(fileLockDetector: mockLockDetector.Object);
        var detectedEvents = new List<FileDetectedEventArgs>();
        monitor.FileDetected += (s, e) => detectedEvents.Add(e);

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Buffer Test Rule",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = _testDirectory, IncludeSubfolders = false }]
        };

        await monitor.StartAsync([rule]);

        // Act: Pause monitoring
        monitor.Pause();
        Assert.True(monitor.IsPaused);

        // Create 5 test files in directory
        for (int i = 1; i <= 5; i++)
        {
            var p = Path.Combine(_testDirectory, $"buffered_{i}.txt");
            File.WriteAllText(p, $"data {i}");
        }

        // Trigger manual scan while paused
        await monitor.ScanAllFoldersAsync();

        // While paused, no events should have been dispatched to FileDetected, but should be buffered
        Assert.Empty(detectedEvents);
        Assert.True(monitor.PausedBufferedEventCount >= 5);

        // Resume monitoring
        await monitor.ResumeAsync();
        Assert.False(monitor.IsPaused);
        Assert.Equal(0, monitor.PausedBufferedEventCount);

        // Events should now have been flushed and delivered
        Assert.True(detectedEvents.Count >= 5);
    }
}
