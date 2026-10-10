using FluentAssertions;
using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Processing;
using TidyUp.Tests.Helpers;

namespace TidyUp.Tests.Unit.Services;

public class FileLockTests : IDisposable
{
    private readonly TestFileHelper _fileHelper;
    private readonly FileLockDetector _detector;
    private readonly ActionExecutor _executor;
    private readonly VariableEngine _variableEngine;

    public FileLockTests()
    {
        _fileHelper = new TestFileHelper();
        _detector = new FileLockDetector();
        _variableEngine = new VariableEngine();
        _executor = new ActionExecutor(_variableEngine, fileLockDetector: _detector);
    }

    public void Dispose()
    {
        _fileHelper.Dispose();
        GC.SuppressFinalize(this);
    }

    #region FileLockDetector Tests

    [Theory]
    [InlineData("sample.crdownload", true)]
    [InlineData("document.part", true)]
    [InlineData("archive.zip.partial", true)]
    [InlineData("video.download", true)]
    [InlineData("cache.tmp", true)]
    [InlineData("scratch.temp", true)]
    [InlineData("~$Proposal.docx", true)]
    [InlineData("~WRL0001.tmp", true)]
    [InlineData(".DS_Store", true)]
    [InlineData("final_report.pdf", false)]
    [InlineData("photo.jpg", false)]
    [InlineData("data.csv", false)]
    [InlineData("backup.zip", false)]
    public void IsTemporaryOrIncompleteFile_DetectsKnownDownloadAndLockPatterns(string filename, bool expectedIncomplete)
    {
        var result = _detector.IsTemporaryOrIncompleteFile(filename);
        result.Should().Be(expectedIncomplete);
    }

    [Fact]
    public void IsFileReady_ReturnsFalse_ForNonExistentFile()
    {
        var nonExistentPath = Path.Combine(_fileHelper.TestRootDirectory, "ghost.txt");
        _detector.IsFileReady(nonExistentPath).Should().BeFalse();
    }

    [Fact]
    public void IsFileReady_ReturnsFalse_ForIncompleteDownloadExtension_EvenIfFileExists()
    {
        var file = _fileHelper.CreateTestFile("downloading.crdownload", "partial data");
        _detector.IsFileReady(file.FullName).Should().BeFalse();
    }

    [Fact]
    public void IsFileReady_DetectsExclusiveLock_AndRecoversWhenStreamClosed()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("locked.txt", "important content");

        // Open exclusive write stream (simulating active download or writer)
        using (var lockStream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Act & Assert while locked
            _detector.IsFileReady(file.FullName).Should().BeFalse("file is held open exclusively");
        }

        // Assert after stream is closed/disposed
        _detector.IsFileReady(file.FullName).Should().BeTrue("file stream was released");
    }

    [Fact]
    public async Task WaitForFileReadyAsync_WaitsAndReturnsTrue_WhenLockReleasedConcurrently()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("delayed_unlock.txt", "content");
        var lockStream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Schedule lock release after 150ms
        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            lockStream.Dispose();
        });

        // Act
        var ready = await _detector.WaitForFileReadyAsync(
            file.FullName,
            timeout: TimeSpan.FromSeconds(2),
            pollInterval: TimeSpan.FromMilliseconds(50));

        // Assert
        ready.Should().BeTrue("lock was released during polling");
    }

    [Fact]
    public async Task WaitForFileReadyAsync_ReturnsFalse_WhenLockRemainsHeldPastTimeout()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("permanent_lock.txt", "content");
        using var lockStream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act
        var ready = await _detector.WaitForFileReadyAsync(
            file.FullName,
            timeout: TimeSpan.FromMilliseconds(200),
            pollInterval: TimeSpan.FromMilliseconds(50));

        // Assert
        ready.Should().BeFalse("lock was held for the entire timeout duration");
    }

    #endregion

    #region ActionExecutor Integration Tests

    [Fact]
    public async Task ActionExecutor_ExecuteMoveAsync_DoesNotMoveOrCorrupt_WhenFileIsLocked()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("locked_source.txt", "original content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        var expectedDestPath = Path.Combine(destDir.FullName, "locked_source.txt");

        // Hold exclusive lock
        using (var lockStream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Act
            var result = await _executor.ExecuteActionAsync(action, file);

            // Assert
            result.Success.Should().BeFalse();
            result.Type.Should().Be(ActionResultType.Error);
            result.ErrorMessage.Should().Contain("locked");

            // Source file must remain completely intact and uncorrupted
            File.Exists(file.FullName).Should().BeTrue("source file must not be removed when locked");
            File.Exists(expectedDestPath).Should().BeFalse("destination must not be created");
        }

        // Verify content after stream release
        File.ReadAllText(file.FullName).Should().Be("original content");
    }

    [Fact]
    public async Task ActionExecutor_ExecuteMoveAsync_Succeeds_OnceLockIsReleased()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("source_to_move.txt", "content to move");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        var expectedDestPath = Path.Combine(destDir.FullName, "source_to_move.txt");

        // 1. Lock file and attempt move
        using (var lockStream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var lockedResult = await _executor.ExecuteActionAsync(action, file);
            lockedResult.Success.Should().BeFalse();
        }

        // 2. Lock is now released; re-attempt move
        var unlockedResult = await _executor.ExecuteActionAsync(action, file);

        // Assert
        unlockedResult.Success.Should().BeTrue();
        unlockedResult.ResultPath.Should().Be(expectedDestPath);
        File.Exists(file.FullName).Should().BeFalse("source file was cleanly moved");
        File.Exists(expectedDestPath).Should().BeTrue("file now exists at destination");
        File.ReadAllText(expectedDestPath).Should().Be("content to move");
    }

    [Fact]
    public async Task ActionExecutor_ExecuteMoveAsync_SkipsIncompleteDownloadFile()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("package.zip.crdownload", "partial data");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, file);

        // Assert
        result.Success.Should().BeFalse();
        result.Type.Should().Be(ActionResultType.Skipped);
        result.ErrorMessage.Should().Contain("incomplete");
        File.Exists(file.FullName).Should().BeTrue("incomplete file was untouched");
    }

    #endregion

    #region RetryQueueManager Tests

    [Fact]
    public async Task RetryQueueManager_EnqueuesLockedFile_AndProcessesWhenLockReleased()
    {
        // Arrange: zero delay for instantaneous deterministic unit testing
        var zeroDelays = new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero };
        using var queueManager = new RetryQueueManager(_detector, backoffDelays: zeroDelays);

        var file = _fileHelper.CreateTestFile("queue_test.txt", "retry data");
        var sourceFolder = _fileHelper.TestRootDirectory;
        FileDetectedEventArgs? readyArgs = null;

        queueManager.FileReady += (_, args) => readyArgs = args;

        // 1. Hold lock open
        using (var lockStream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            queueManager.Enqueue(file.FullName, sourceFolder, maxRetries: 3);
            queueManager.QueueCount.Should().Be(1);

            // Process queue while still locked
            await queueManager.ProcessQueueAsync();

            readyArgs.Should().BeNull("file is still locked");
            queueManager.QueueCount.Should().Be(1);
        }

        // 2. Lock is now released. Process queue again
        await queueManager.ProcessQueueAsync();

        // Assert
        readyArgs.Should().NotBeNull("file should be signaled as ready once unlocked");
        readyArgs!.FileInfo.FullName.Should().Be(file.FullName);
        readyArgs.SourceFolder.Should().Be(sourceFolder);
        queueManager.QueueCount.Should().Be(0, "item was completed and removed from queue");
    }

    [Fact]
    public async Task RetryQueueManager_FiresFailedEvent_WhenMaxRetriesExceeded()
    {
        // Arrange: 2 retries with zero delay
        var zeroDelays = new[] { TimeSpan.Zero, TimeSpan.Zero };
        using var queueManager = new RetryQueueManager(_detector, backoffDelays: zeroDelays);

        var file = _fileHelper.CreateTestFile("never_unlocked.txt", "locked data");
        RetryFailedEventArgs? failedArgs = null;
        queueManager.FileRetryFailed += (_, args) => failedArgs = args;

        // Hold lock throughout
        using var lockStream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        queueManager.Enqueue(file.FullName, _fileHelper.TestRootDirectory, maxRetries: 2);

        // Attempt 1: locked -> attemptCount = 1
        await queueManager.ProcessQueueAsync();
        failedArgs.Should().BeNull();
        queueManager.QueueCount.Should().Be(1);

        // Attempt 2: locked -> attemptCount = 2 (max reached) -> expires
        await queueManager.ProcessQueueAsync();

        // Assert
        failedArgs.Should().NotBeNull();
        failedArgs!.FilePath.Should().Be(file.FullName);
        failedArgs.AttemptCount.Should().Be(2);
        queueManager.QueueCount.Should().Be(0, "item removed from queue after exceeding retries");
    }

    #endregion

    #region FileMonitorService Integration Tests

    [Fact]
    public async Task FileMonitorService_ReceivesUnlockedFileEvent_From_RetryQueue()
    {
        // Arrange
        var zeroDelays = new[] { TimeSpan.Zero };
        using var queueManager = new RetryQueueManager(_detector, backoffDelays: zeroDelays);
        using var monitorService = new FileMonitorService(_detector, queueManager);

        FileDetectedEventArgs? detectedEventArgs = null;
        monitorService.FileDetected += (_, args) => detectedEventArgs = args;

        var file = _fileHelper.CreateTestFile("monitored_file.txt", "ready data");

        // Act: simulate queue manager completing an unlocked file
        queueManager.Enqueue(file.FullName, _fileHelper.TestRootDirectory);
        await queueManager.ProcessQueueAsync();

        // Assert
        detectedEventArgs.Should().NotBeNull("FileMonitorService should forward ready events from the retry queue");
        detectedEventArgs!.FileInfo.FullName.Should().Be(file.FullName);
    }

    #endregion
}
