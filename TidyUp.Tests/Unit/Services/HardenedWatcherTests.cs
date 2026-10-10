using FluentAssertions;
using System.Collections.Concurrent;
using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Services;
using TidyUp.Services.Watcher;
using TidyUp.Tests.Helpers;

namespace TidyUp.Tests.Unit.Services;

public class HardenedWatcherTests : IDisposable
{
    private readonly TestFileHelper _fileHelper;

    public HardenedWatcherTests()
    {
        _fileHelper = new TestFileHelper();
    }

    public void Dispose()
    {
        _fileHelper.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DefaultConfiguration_Allocates64KBInternalBuffer()
    {
        // Arrange & Act
        using var watcher = new HardenedFileSystemWatcher(_fileHelper.TestRootDirectory);

        // Assert
        watcher.InternalBufferSize.Should().Be(65536, "64KB is the maximum reliable non-paged memory buffer on Windows");
        watcher.WatchedPath.Should().Be(_fileHelper.TestRootDirectory);
        watcher.IsRunning.Should().BeFalse();

        watcher.Start();
        watcher.IsRunning.Should().BeTrue();

        watcher.Stop();
        watcher.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void CustomConfiguration_AppliesOptionsCorrectly()
    {
        // Arrange
        var options = new HardenedWatcherOptions
        {
            InternalBufferSize = 32768,
            DebounceDelay = TimeSpan.FromMilliseconds(200),
            IncludeSubdirectories = true
        };

        // Act
        using var watcher = new HardenedFileSystemWatcher(_fileHelper.TestRootDirectory, options);

        // Assert
        watcher.InternalBufferSize.Should().Be(32768);
        watcher.WatchedPath.Should().Be(_fileHelper.TestRootDirectory);
    }

    [Fact]
    public void BufferOverflow_Simulated_RaisesBufferOverflowAndReconciliationRequested()
    {
        // Arrange
        using var watcher = new HardenedFileSystemWatcher(_fileHelper.TestRootDirectory);
        ErrorEventArgs? receivedError = null;
        string? reconciliationPath = null;

        watcher.BufferOverflow += (s, e) => receivedError = e;
        watcher.ReconciliationRequested += (s, path) => reconciliationPath = path;

        // Act
        watcher.SimulateBufferOverflow();

        // Assert
        receivedError.Should().NotBeNull();
        receivedError!.GetException().Should().BeOfType<InternalBufferOverflowException>();
        receivedError.GetException().Message.Should().Contain("Simulated 64KB buffer overflow");
        reconciliationPath.Should().Be(_fileHelper.TestRootDirectory);
    }

    [Fact]
    public void TriggerReconciliation_ExplicitCall_RaisesReconciliationRequested()
    {
        // Arrange
        using var watcher = new HardenedFileSystemWatcher(_fileHelper.TestRootDirectory);
        string? requestedPath = null;
        watcher.ReconciliationRequested += (s, path) => requestedPath = path;

        // Act
        watcher.TriggerReconciliation();

        // Assert
        requestedPath.Should().Be(_fileHelper.TestRootDirectory);
    }

    [Fact]
    public async Task Debounce_RapidWritesToSameFile_CoalescesToSingleEvent()
    {
        // Arrange
        var options = new HardenedWatcherOptions
        {
            DebounceDelay = TimeSpan.FromMilliseconds(150)
        };
        using var watcher = new HardenedFileSystemWatcher(_fileHelper.TestRootDirectory, options);
        var detectedFiles = new ConcurrentBag<string>();
        watcher.FileDetected += (s, path) => detectedFiles.Add(path);

        watcher.Start();

        var filePath = Path.Combine(_fileHelper.TestRootDirectory, "rapid_burst.txt");

        // Act: Create and write rapidly 5 times
        File.WriteAllText(filePath, "burst 1");
        for (int i = 2; i <= 5; i++)
        {
            await Task.Delay(20);
            File.AppendAllText(filePath, $" burst {i}");
        }

        // Wait for debounce delay (150ms) plus buffer
        await Task.Delay(400);

        // Assert: All 5 rapid file modifications are coalesced into a single notification
        detectedFiles.Should().ContainSingle(f => f.Equals(filePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RenamedEvent_PairsAndEmitsNewPath()
    {
        // Arrange
        var options = new HardenedWatcherOptions
        {
            DebounceDelay = TimeSpan.FromMilliseconds(100)
        };
        using var watcher = new HardenedFileSystemWatcher(_fileHelper.TestRootDirectory, options);
        var detectedFiles = new ConcurrentBag<string>();
        watcher.FileDetected += (s, path) => detectedFiles.Add(path);

        watcher.Start();

        var tempFilePath = Path.Combine(_fileHelper.TestRootDirectory, "download.crdownload");
        var finalFilePath = Path.Combine(_fileHelper.TestRootDirectory, "download.zip");

        // Act: Create temp file then rename to final file
        File.WriteAllText(tempFilePath, "content");
        await Task.Delay(50);
        File.Move(tempFilePath, finalFilePath);

        // Wait for debounce
        await Task.Delay(350);

        // Assert
        detectedFiles.Should().Contain(f => f.Equals(finalFilePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ConcurrentBurst500Files_ProcessesAllFilesWithoutEventLoss()
    {
        // Arrange: Monitored folder with 64KB buffer and 150ms per-path throttle
        var options = new HardenedWatcherOptions
        {
            InternalBufferSize = 65536,
            DebounceDelay = TimeSpan.FromMilliseconds(150)
        };
        using var watcher = new HardenedFileSystemWatcher(_fileHelper.TestRootDirectory, options);
        var detectedFiles = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        watcher.FileDetected += (s, path) => detectedFiles.TryAdd(path, 0);
        watcher.Start();

        const int fileCount = 500;
        var createdPaths = new List<string>(fileCount);

        for (int i = 0; i < fileCount; i++)
        {
            createdPaths.Add(Path.Combine(_fileHelper.TestRootDirectory, $"burst_item_{i:D4}.dat"));
        }

        // Act: Concurrently create 500 files to simulate heavy burst
        Parallel.ForEach(createdPaths, path =>
        {
            File.WriteAllText(path, "burst data payload");
        });

        // Wait up to 10 seconds for all 500 distinct paths to be debounced and emitted
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (detectedFiles.Count < fileCount && DateTime.UtcNow < timeout)
        {
            await Task.Delay(50);
        }

        // Assert
        detectedFiles.Count.Should().Be(fileCount, "every file in the 500-file burst must be debounced and delivered without event loss");
        foreach (var path in createdPaths)
        {
            detectedFiles.ContainsKey(path).Should().BeTrue($"file '{path}' should be in detected list");
        }
    }

    [Fact]
    public async Task FileMonitorService_ReconcileFolderAsync_InvokesReconciliationAndDetectsExistingFiles()
    {
        // Arrange
        using var service = new FileMonitorService();
        var triggeredPath = string.Empty;
        service.ReconciliationTriggered += (s, path) => triggeredPath = path;

        var detectedFiles = new ConcurrentBag<string>();
        service.FileDetected += (s, e) => detectedFiles.Add(e.FileInfo.FullName);

        // Pre-create 3 test files in folder
        var file1 = _fileHelper.CreateTestFile("existing_file1.txt", "content 1");
        var file2 = _fileHelper.CreateTestFile("existing_file2.txt", "content 2");
        var file3 = _fileHelper.CreateTestFile("existing_file3.txt", "content 3");

        // Act: Reconcile folder explicitly
        await service.ReconcileFolderAsync(_fileHelper.TestRootDirectory, includeSubfolders: false, exclusionPatterns: Array.Empty<string>());

        // Assert
        triggeredPath.Should().Be(_fileHelper.TestRootDirectory);
        detectedFiles.Should().Contain(file1.FullName);
        detectedFiles.Should().Contain(file2.FullName);
        detectedFiles.Should().Contain(file3.FullName);
    }
}

