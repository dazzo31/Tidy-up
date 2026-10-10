using FluentAssertions;
using System.IO;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data;
using TidyUp.Data.Entities;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Rollback;
using TidyUp.Tests.Helpers;

namespace TidyUp.Tests.Unit.Services;

public class RollbackEngineTests : IDisposable
{
    private readonly TestFileHelper _fileHelper;
    private readonly TidyUpDbContext _dbContext;
    private readonly RollbackEngine _rollbackEngine;
    private readonly ActionExecutor _actionExecutor;
    private readonly VariableEngine _variableEngine;

    public RollbackEngineTests()
    {
        _fileHelper = new TestFileHelper();

        var dbPath = Path.Combine(_fileHelper.TestRootDirectory, "test_journal.db");
        var options = new DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _dbContext = new TidyUpDbContext(options);
        _dbContext.Database.EnsureCreated();

        _rollbackEngine = new RollbackEngine(directContext: _dbContext);
        _variableEngine = new VariableEngine();
        _actionExecutor = new ActionExecutor(
            _variableEngine,
            fileLockDetector: new FileLockDetector(),
            rollbackEngine: _rollbackEngine);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _fileHelper.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RollbackMove_ReversesToOriginalPath_WhenTargetUnmodified()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("source_document.txt", "Original Important Text");
        var destDir = _fileHelper.CreateTestDirectory("archive");
        var moveAction = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        var batchId = Guid.NewGuid();
        var execResult = await _actionExecutor.ExecuteActionAsync(moveAction, sourceFile, batchId: batchId);
        execResult.Success.Should().BeTrue();

        var targetPath = Path.Combine(destDir.FullName, "source_document.txt");
        File.Exists(sourceFile.FullName).Should().BeFalse();
        File.Exists(targetPath).Should().BeTrue();

        var journalEntries = await _rollbackEngine.GetBatchEntriesAsync(batchId);
        journalEntries.Should().HaveCount(1);
        var entry = journalEntries[0];
        entry.ActionType.Should().Be("Move");
        entry.Status.Should().Be("Completed");

        // Act
        var rollbackResult = await _rollbackEngine.RollbackOperationAsync(entry.OperationId);

        // Assert
        rollbackResult.Success.Should().BeTrue();
        rollbackResult.OriginalPath.Should().Be(sourceFile.FullName);
        File.Exists(sourceFile.FullName).Should().BeTrue("file must be restored to original path");
        File.Exists(targetPath).Should().BeFalse("file must be removed from target path");
        File.ReadAllText(sourceFile.FullName).Should().Be("Original Important Text");

        var updatedEntry = await _rollbackEngine.GetEntryAsync(entry.OperationId);
        updatedEntry.Should().NotBeNull();
        updatedEntry!.Status.Should().Be("RolledBack");
        updatedEntry.RolledBackAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RollbackMove_RefusesToOverwriteAndReportsConflict_WhenTargetModifiedAfterMove()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("budget.xlsx", "Initial Budget Data");
        var destDir = _fileHelper.CreateTestDirectory("finance");
        var moveAction = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        var batchId = Guid.NewGuid();
        await _actionExecutor.ExecuteActionAsync(moveAction, sourceFile, batchId: batchId);

        var targetPath = Path.Combine(destDir.FullName, "budget.xlsx");
        var journalEntries = await _rollbackEngine.GetBatchEntriesAsync(batchId);
        var entry = journalEntries[0];

        // Simulate external user editing target file after TidyUp moved it
        File.AppendAllText(targetPath, "\nUser added crucial revenue modifications!");

        // Act
        var rollbackResult = await _rollbackEngine.RollbackOperationAsync(entry.OperationId);

        // Assert
        rollbackResult.Success.Should().BeFalse("rollback must refuse to overwrite user-edited files");
        rollbackResult.ConflictReason.Should().Be(RollbackConflictReason.TargetModifiedByExternalProcess);
        rollbackResult.Message.Should().Contain("modified");

        // Target file must be preserved intact with user changes
        File.Exists(targetPath).Should().BeTrue();
        File.ReadAllText(targetPath).Should().Contain("User added crucial revenue modifications!");
        File.Exists(sourceFile.FullName).Should().BeFalse("original path must not be touched");

        var updatedEntry = await _rollbackEngine.GetEntryAsync(entry.OperationId);
        updatedEntry!.Status.Should().Be("Completed", "status remains Completed because rollback was aborted");
    }

    [Fact]
    public async Task RollbackMove_RefusesToClobber_WhenOriginalLocationOccupied()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("item.txt", "Initial Item Text");
        var destDir = _fileHelper.CreateTestDirectory("output");
        var moveAction = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        var batchId = Guid.NewGuid();
        await _actionExecutor.ExecuteActionAsync(moveAction, sourceFile, batchId: batchId);
        var entry = (await _rollbackEngine.GetBatchEntriesAsync(batchId))[0];

        // Simulate new file placed at original path
        File.WriteAllText(sourceFile.FullName, "Another file occupying original path");

        // Act
        var rollbackResult = await _rollbackEngine.RollbackOperationAsync(entry.OperationId);

        // Assert
        rollbackResult.Success.Should().BeFalse("rollback must not clobber occupied destination");
        rollbackResult.ConflictReason.Should().Be(RollbackConflictReason.OriginalDestinationOccupied);
        rollbackResult.Message.Should().Contain("occupied");

        File.ReadAllText(sourceFile.FullName).Should().Be("Another file occupying original path");
        File.Exists(Path.Combine(destDir.FullName, "item.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task RollbackMove_ReportsMissing_WhenTargetDeletedExternally()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("deleted_later.txt", "Content");
        var destDir = _fileHelper.CreateTestDirectory("target_dir");
        var moveAction = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        var batchId = Guid.NewGuid();
        await _actionExecutor.ExecuteActionAsync(moveAction, sourceFile, batchId: batchId);
        var entry = (await _rollbackEngine.GetBatchEntriesAsync(batchId))[0];

        // Delete target file externally
        File.Delete(Path.Combine(destDir.FullName, "deleted_later.txt"));

        // Act
        var rollbackResult = await _rollbackEngine.RollbackOperationAsync(entry.OperationId);

        // Assert
        rollbackResult.Success.Should().BeFalse();
        rollbackResult.ConflictReason.Should().Be(RollbackConflictReason.TargetMissing);
        rollbackResult.Message.Should().Contain("no longer exists");
    }

    [Fact]
    public async Task RollbackDelete_DocumentsRecycleBinRestorationInstructions()
    {
        // Arrange
        var filePath = Path.Combine(_fileHelper.TestRootDirectory, "safe_to_trash.docx");
        var entry = new OperationJournalEntry
        {
            ActionType = "Delete",
            OriginalPath = filePath,
            Status = "Completed",
            Details = "Sent to Recycle Bin"
        };
        await _rollbackEngine.RecordOperationAsync(entry);

        // Act
        var rollbackResult = await _rollbackEngine.RollbackOperationAsync(entry.OperationId);

        // Assert
        rollbackResult.Success.Should().BeTrue();
        rollbackResult.ConflictReason.Should().Be(RollbackConflictReason.RecycleBinRestorationGuidance);
        rollbackResult.RestorationInstructions.Should().NotBeNullOrWhiteSpace();
        rollbackResult.RestorationInstructions.Should().Contain("Recycle Bin");
        rollbackResult.RestorationInstructions.Should().Contain("Restore");
        rollbackResult.RestorationInstructions.Should().Contain("safe_to_trash.docx");
    }

    [Fact]
    public async Task RollbackDelete_ReportsConflict_WhenOriginalPathOccupied()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("occupied_trash.txt", "Some content");
        var entry = new OperationJournalEntry
        {
            ActionType = "Delete",
            OriginalPath = file.FullName,
            Status = "Completed",
            Details = "Sent to Recycle Bin"
        };
        await _rollbackEngine.RecordOperationAsync(entry);

        // Act
        var rollbackResult = await _rollbackEngine.RollbackOperationAsync(entry.OperationId);

        // Assert
        rollbackResult.Success.Should().BeFalse();
        rollbackResult.ConflictReason.Should().Be(RollbackConflictReason.OriginalDestinationOccupied);
        rollbackResult.Message.Should().Contain("occupied");
    }

    [Fact]
    public async Task RollbackCopy_RemovesCopiedFile_WhenUnmodified()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("source_copy.txt", "Copy this text");
        var destDir = _fileHelper.CreateTestDirectory("copied_dir");
        var copyAction = new CopyFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        var batchId = Guid.NewGuid();
        var execResult = await _actionExecutor.ExecuteActionAsync(copyAction, sourceFile, batchId: batchId);
        execResult.Success.Should().BeTrue();

        var targetPath = Path.Combine(destDir.FullName, "source_copy.txt");
        File.Exists(sourceFile.FullName).Should().BeTrue("source remains in copy");
        File.Exists(targetPath).Should().BeTrue("copy created at destination");

        var entry = (await _rollbackEngine.GetBatchEntriesAsync(batchId))[0];

        // Act
        var rollbackResult = await _rollbackEngine.RollbackOperationAsync(entry.OperationId);

        // Assert
        rollbackResult.Success.Should().BeTrue();
        File.Exists(targetPath).Should().BeFalse("copied file was deleted on rollback");
        File.Exists(sourceFile.FullName).Should().BeTrue("original source remains intact");
    }

    [Fact]
    public async Task RollbackBatch_ReversesAllOperationsInBatch_InReverseChronologicalOrder()
    {
        // Arrange
        var file1 = _fileHelper.CreateTestFile("batch1.txt", "Text 1");
        var file2 = _fileHelper.CreateTestFile("batch2.txt", "Text 2");
        var file3 = _fileHelper.CreateTestFile("batch3.txt", "Text 3");

        var destDir = _fileHelper.CreateTestDirectory("batch_dest");
        var batchId = Guid.NewGuid();

        await _actionExecutor.ExecuteActionAsync(new MoveFileAction { DestinationPath = destDir.FullName }, file1, batchId: batchId);
        await _actionExecutor.ExecuteActionAsync(new MoveFileAction { DestinationPath = destDir.FullName }, file2, batchId: batchId);
        await _actionExecutor.ExecuteActionAsync(new MoveFileAction { DestinationPath = destDir.FullName }, file3, batchId: batchId);

        // Act
        var batchResult = await _rollbackEngine.RollbackBatchAsync(batchId);

        // Assert
        batchResult.OverallSuccess.Should().BeTrue();
        batchResult.TotalOperations.Should().Be(3);
        batchResult.RolledBackCount.Should().Be(3);
        batchResult.FailedCount.Should().Be(0);

        File.Exists(file1.FullName).Should().BeTrue();
        File.Exists(file2.FullName).Should().BeTrue();
        File.Exists(file3.FullName).Should().BeTrue();

        File.Exists(Path.Combine(destDir.FullName, "batch1.txt")).Should().BeFalse();
        File.Exists(Path.Combine(destDir.FullName, "batch2.txt")).Should().BeFalse();
        File.Exists(Path.Combine(destDir.FullName, "batch3.txt")).Should().BeFalse();
    }

    [Fact]
    public async Task RollbackOperation_ReturnsAlreadyRolledBack_OnSecondAttempt()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("twice.txt", "Double rollback test");
        var destDir = _fileHelper.CreateTestDirectory("twice_dest");
        var batchId = Guid.NewGuid();

        await _actionExecutor.ExecuteActionAsync(new MoveFileAction { DestinationPath = destDir.FullName }, sourceFile, batchId: batchId);
        var entry = (await _rollbackEngine.GetBatchEntriesAsync(batchId))[0];

        // 1. First rollback
        var firstRollback = await _rollbackEngine.RollbackOperationAsync(entry.OperationId);
        firstRollback.Success.Should().BeTrue();

        // 2. Second rollback on same operation
        var secondRollback = await _rollbackEngine.RollbackOperationAsync(entry.OperationId);

        // Assert
        secondRollback.Success.Should().BeFalse();
        secondRollback.ConflictReason.Should().Be(RollbackConflictReason.AlreadyRolledBack);
        secondRollback.Message.Should().Contain("already been rolled back");
    }
}

