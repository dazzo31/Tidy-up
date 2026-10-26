using Moq;
using TidyUp.Services.FileSystem;
using FluentAssertions;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Tests.Helpers;

namespace TidyUp.Tests.Unit.Services;

public class ActionExecutorTests : IDisposable
{
    private readonly ActionExecutor _executor;
    private readonly VariableEngine _variableEngine;
    private readonly TestFileHelper _fileHelper;

    public ActionExecutorTests()
    {
        _variableEngine = new VariableEngine();
        _executor = new ActionExecutor(_variableEngine);
        _fileHelper = new TestFileHelper();
    }

    #region Move Tests

    [Fact]
    public async Task ExecuteMoveAsync_ValidPath_MovesFile()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        result.Type.Should().Be(ActionResultType.Success);
        File.Exists(sourceFile.FullName).Should().BeFalse();
        File.Exists(Path.Combine(destDir.FullName, "test.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteMoveAsync_ConflictWithSkip_SkipsOperation()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "source content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        _fileHelper.CreateTestFile("destination/test.txt", "existing content");
        
        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeFalse();
        result.Type.Should().Be(ActionResultType.Skipped);
        File.Exists(sourceFile.FullName).Should().BeTrue(); // Source still exists
        File.ReadAllText(Path.Combine(destDir.FullName, "test.txt")).Should().Be("existing content"); // Dest unchanged
    }

    [Fact]
    public async Task ExecuteMoveAsync_ConflictWithOverwrite_ReplacesFile()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "source content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        _fileHelper.CreateTestFile("destination/test.txt", "existing content");
        
        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Overwrite
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue($"Error: {result.ErrorMessage}");
        File.ReadAllText(Path.Combine(destDir.FullName, "test.txt")).Should().Be("source content");
    }

    [Fact]
    public async Task ExecuteMoveAsync_ConflictWithRenameNew_CreatesNewFile()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "source content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        _fileHelper.CreateTestFile("destination/test.txt", "existing content");
        
        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.RenameNew
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(Path.Combine(destDir.FullName, "test.txt")).Should().BeTrue();
        File.Exists(Path.Combine(destDir.FullName, "test(1).txt")).Should().BeTrue();
    }

    #endregion

    #region Copy Tests

    [Fact]
    public async Task ExecuteCopyAsync_ValidPath_CopiesFile()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        var action = new CopyFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(sourceFile.FullName).Should().BeTrue(); // Source still exists
        File.Exists(Path.Combine(destDir.FullName, "test.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteCopyAsync_ApplyToSourceFile_ReturnsSourcePath()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        var action = new CopyFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip,
            ApplyToSourceFile = true
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.ResultPath.Should().Be(sourceFile.FullName);
    }

    [Fact]
    public async Task ExecuteCopyAsync_ApplyToCopiedFile_ReturnsDestPath()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        var action = new CopyFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip,
            ApplyToSourceFile = false
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.ResultPath.Should().Be(Path.Combine(destDir.FullName, "test.txt"));
    }

    #endregion

    #region Rename Tests

    [Fact]
    public async Task ExecuteRenameAsync_ValidPattern_RenamesFile()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var action = new RenameFileAction
        {
            NamePattern = "renamed",
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(sourceFile.FullName).Should().BeFalse();
        File.Exists(Path.Combine(sourceFile.DirectoryName!, "renamed.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteRenameAsync_PatternWithVariables_ResolvesVariables()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var action = new RenameFileAction
        {
            NamePattern = "{filename}_backup",
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(Path.Combine(sourceFile.DirectoryName!, "test_backup.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteRenameAsync_PatternWithoutExtension_AddsExtension()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var action = new RenameFileAction
        {
            NamePattern = "newname",
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(Path.Combine(sourceFile.DirectoryName!, "newname.txt")).Should().BeTrue();
    }

    #endregion

    #region ChangeExtension Tests

    [Fact]
    public async Task ExecuteChangeExtensionAsync_ValidExtension_ChangesExtension()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var action = new ChangeExtensionAction
        {
            NewExtension = "bak",
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(sourceFile.FullName).Should().BeFalse();
        File.Exists(Path.Combine(sourceFile.DirectoryName!, "test.bak")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteChangeExtensionAsync_ExtensionWithDot_HandlesCorrectly()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var action = new ChangeExtensionAction
        {
            NewExtension = ".bak",
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(Path.Combine(sourceFile.DirectoryName!, "test.bak")).Should().BeTrue();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task ExecuteDeleteAsync_WithoutRecycleBin_DeletesPermanently()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var action = new DeleteFileAction
        {
            UseRecycleBin = false,
            ConfirmBeforeDelete = false
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(sourceFile.FullName).Should().BeFalse();
    }

    #endregion

    #region Preview Tests

    [Fact]
    public async Task PreviewActionsAsync_MoveAction_ReturnsCorrectPreview()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        var actions = new List<FileAction>
        {
            new MoveFileAction
            {
                DestinationPath = destDir.FullName,
                ConflictResolution = ConflictResolution.Skip
            }
        };

        // Act
        var previews = await _executor.PreviewActionsAsync(actions, sourceFile);

        // Assert
        previews.Should().HaveCount(1);
        previews[0].ActionType.Should().Be("Move");
        previews[0].Description.Should().Contain(destDir.FullName);
        previews[0].HasConflict.Should().BeFalse();
    }

    [Fact]
    public async Task PreviewActionsAsync_RenameAction_ReturnsCorrectPreview()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var actions = new List<FileAction>
        {
            new RenameFileAction
            {
                NamePattern = "renamed"
            }
        };

        // Act
        var previews = await _executor.PreviewActionsAsync(actions, sourceFile);

        // Assert
        previews.Should().HaveCount(1);
        previews[0].ActionType.Should().Be("Rename");
        previews[0].Description.Should().Contain("renamed.txt");
    }

    [Fact]
    public async Task PreviewActionsAsync_MultipleActions_PreviewsAllInOrder()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        var actions = new List<FileAction>
        {
            new RenameFileAction { Order = 1, NamePattern = "renamed" },
            new MoveFileAction { Order = 2, DestinationPath = destDir.FullName }
        };

        // Act
        var previews = await _executor.PreviewActionsAsync(actions, sourceFile);

        // Assert
        previews.Should().HaveCount(2);
        previews[0].ActionType.Should().Be("Rename");
        previews[1].ActionType.Should().Be("Move");
    }

    [Fact]
    public async Task PreviewActionsAsync_DetectsConflicts()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        _fileHelper.CreateTestFile("destination/test.txt", "existing");
        
        var actions = new List<FileAction>
        {
            new MoveFileAction
            {
                DestinationPath = destDir.FullName,
                ConflictResolution = ConflictResolution.Skip
            }
        };

        // Act
        var previews = await _executor.PreviewActionsAsync(actions, sourceFile);

        // Assert
        previews[0].HasConflict.Should().BeTrue();
        previews[0].ConflictResolution.Should().Be("Skip");
    }

    #endregion

    #region ExecuteActions Tests

    [Fact]
    public async Task ExecuteActionsAsync_MultipleActions_ExecutesInOrder()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var actions = new List<FileAction>
        {
            new RenameFileAction { Order = 1, NamePattern = "renamed" },
            new ChangeExtensionAction { Order = 2, NewExtension = "bak" }
        };

        // Act
        var results = await _executor.ExecuteActionsAsync(actions, sourceFile);

        // Assert
        results.Should().HaveCount(2);
        results.All(r => r.Success).Should().BeTrue();
        File.Exists(sourceFile.FullName).Should().BeFalse();
        File.Exists(Path.Combine(sourceFile.DirectoryName!, "renamed.bak")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteActionsAsync_ActionFails_StopsExecution()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("test.txt", "test content");
        var destDir = _fileHelper.CreateTestDirectory("destination");
        _fileHelper.CreateTestFile("destination/test.txt", "existing");
        
        var actions = new List<FileAction>
        {
            new MoveFileAction 
            { 
                Order = 1, 
                DestinationPath = destDir.FullName,
                ConflictResolution = ConflictResolution.Skip  // Will fail
            },
            new DeleteFileAction { Order = 2, UseRecycleBin = false }  // Should not execute
        };

        // Act
        var results = await _executor.ExecuteActionsAsync(actions, sourceFile);

        // Assert
        results.Should().HaveCount(1); // Only first action attempted
        results[0].Success.Should().BeFalse();
        File.Exists(sourceFile.FullName).Should().BeTrue(); // File not deleted
    }

    #endregion


    #region PreserveSubfolderStructure and Planned Execution Tests

    [Fact]
    public async Task ExecuteMoveAsync_WithPreserveSubfolderStructure_MovesFileToNestedDestination()
    {
        // Arrange
        var rootDir = _fileHelper.CreateTestDirectory("MonitoredRoot");
        var nestedDir = Directory.CreateDirectory(Path.Combine(rootDir.FullName, "Sub1", "Sub2"));
        var sourceFilePath = Path.Combine(nestedDir.FullName, "deep.txt");
        await File.WriteAllTextAsync(sourceFilePath, "deep content");
        var sourceFile = new FileInfo(sourceFilePath);

        var destDir = _fileHelper.CreateTestDirectory("DestRoot");
        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            PreserveSubfolderStructure = true,
            MonitoredSourceRoot = rootDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        var expectedDest = Path.Combine(destDir.FullName, "Sub1", "Sub2", "deep.txt");
        File.Exists(expectedDest).Should().BeTrue();
        File.Exists(sourceFilePath).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteCopyAsync_WithPreserveSubfolderStructure_CopiesFileToNestedDestination()
    {
        // Arrange
        var rootDir = _fileHelper.CreateTestDirectory("MonitoredCopyRoot");
        var nestedDir = Directory.CreateDirectory(Path.Combine(rootDir.FullName, "Docs"));
        var sourceFilePath = Path.Combine(nestedDir.FullName, "copy_deep.txt");
        await File.WriteAllTextAsync(sourceFilePath, "deep copy content");
        var sourceFile = new FileInfo(sourceFilePath);

        var destDir = _fileHelper.CreateTestDirectory("DestCopyRoot");
        var action = new CopyFileAction
        {
            DestinationPath = destDir.FullName,
            PreserveSubfolderStructure = true,
            MonitoredSourceRoot = rootDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        var expectedDest = Path.Combine(destDir.FullName, "Docs", "copy_deep.txt");
        File.Exists(expectedDest).Should().BeTrue();
        File.Exists(sourceFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task ExecutePlannedActionAsync_WithPlannedTargetPath_ExecutesToExactDestination()
    {
        // Arrange
        var destDir = _fileHelper.CreateTestDirectory("PlannedDest");
        var sourceFile = _fileHelper.CreateTestFile("planned_source.txt", "planned data");
        var targetPath = Path.Combine(destDir.FullName, "Nested", "final_output.txt");

        var moveAction = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        var planned = new PlannedFileAction
        {
            SourcePath = sourceFile.FullName,
            TargetPath = targetPath,
            FileAction = moveAction,
            ActionType = ActionType.Move
        };

        // Act
        var result = await _executor.ExecutePlannedActionAsync(planned);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(targetPath).Should().BeTrue();
        File.Exists(sourceFile.FullName).Should().BeFalse();
    }

    [Fact]
    public async Task ExecutePlannedActionAsync_EscapingTargetPath_FailsConfinement()
    {
        // Arrange
        var destDir = _fileHelper.CreateTestDirectory("ConfinedDest");
        var escapeDir = _fileHelper.CreateTestDirectory("OutsideDest");
        var sourceFile = _fileHelper.CreateTestFile("escape_source.txt", "escape data");
        var targetPath = Path.Combine(escapeDir.FullName, "escaped.txt");

        var moveAction = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Skip
        };

        var planned = new PlannedFileAction
        {
            SourcePath = sourceFile.FullName,
            TargetPath = targetPath,
            FileAction = moveAction,
            ActionType = ActionType.Move
        };

        // Act
        var result = await _executor.ExecutePlannedActionAsync(planned);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("escapes base destination");
        File.Exists(sourceFile.FullName).Should().BeTrue();
    }

    #endregion

    public void Dispose()
    {
        _fileHelper.Dispose();
    }

    #region TASK-REM-04 Overwrite and Prompt Conflict Tests

    [Fact]
    public async Task ExecuteMoveAsync_ConflictWithPrompt_FailsClosedWithWarning()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("source_prompt.txt", "Source Data");
        var destDir = _fileHelper.CreateTestDirectory("dest_prompt");
        var existingTarget = _fileHelper.CreateTestFile(Path.Combine(destDir.Name, "source_prompt.txt"), "Original Target Data");

        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Prompt
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert: TU-17 resolved - fail closed with Warning, never silently overwrite
        result.Success.Should().BeFalse();
        result.Type.Should().Be(ActionResultType.Warning);
        result.ErrorMessage.Should().Contain("Interactive prompt conflict resolution not supported");
        File.ReadAllText(existingTarget.FullName).Should().Be("Original Target Data", "existing destination file must be completely untouched");
    }

    [Fact]
    public async Task ExecuteMoveAsync_ConflictWithOverwrite_WhenMutationFails_RestoresDisplacedDestinationFile()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("source_fail.txt", "Source Data");
        var destDir = _fileHelper.CreateTestDirectory("dest_fail");
        var targetPath = Path.Combine(destDir.FullName, "source_fail.txt");
        await File.WriteAllTextAsync(targetPath, "Original Important Destination Content");

        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Overwrite
        };

        // Lock source file exclusively so File.Move in ActionExecutor fails after conflict resolution
        using (var lockStream = File.Open(sourceFile.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Act
            var result = await _executor.ExecuteActionAsync(action, sourceFile);

            // Assert
            result.Success.Should().BeFalse();
        }

        // Verify destination file was restored immediately and preserved
        File.Exists(targetPath).Should().BeTrue("displaced destination file must be restored if move mutation fails");
        File.ReadAllText(targetPath).Should().Be("Original Important Destination Content");
    }

    [Fact]
    public async Task ExecuteCopyAsync_ConflictWithOverwrite_RetainsDisplacedFileBackup()
    {
        // Arrange
        var sourceFile = _fileHelper.CreateTestFile("source_copy.txt", "New Source Content");
        var destDir = _fileHelper.CreateTestDirectory("dest_copy");
        var targetPath = Path.Combine(destDir.FullName, "source_copy.txt");
        await File.WriteAllTextAsync(targetPath, "Old Target Content");

        var action = new CopyFileAction
        {
            DestinationPath = destDir.FullName,
            ConflictResolution = ConflictResolution.Overwrite
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        result.DisplacedBackupPath.Should().NotBeNullOrWhiteSpace();
        File.ReadAllText(targetPath).Should().Be("New Source Content");
        File.Exists(result.DisplacedBackupPath).Should().BeTrue();
        File.ReadAllText(result.DisplacedBackupPath!).Should().Be("Old Target Content");
    }

    #endregion
    #region Extract Archive Tests

    [Fact]
    public async Task ExecuteExtractArchiveAsync_WithDeleteAfterExtraction_CallsSafeFileSystemWithRecycleBin()
    {
        // Arrange
        var mockSafeFs = new Mock<ISafeFileSystem>();
        var executor = new ActionExecutor(_variableEngine, safeFileSystem: mockSafeFs.Object);

        var tempDir = _fileHelper.CreateTestDirectory("extract_mock");
        var zipPath = Path.Combine(tempDir.FullName, "mock.zip");
        var contentDir = _fileHelper.CreateTestDirectory("mock_content");
        _fileHelper.CreateTestFile(Path.Combine("mock_content", "sample.txt"), "data");
        System.IO.Compression.ZipFile.CreateFromDirectory(contentDir.FullName, zipPath);

        var targetDest = Path.Combine(tempDir.FullName, "mock_output");
        var action = new ExtractArchiveAction
        {
            DestinationPath = targetDest,
            DeleteAfterExtraction = true,
            UseRecycleBinForArchiveDeletion = true
        };

        // Act
        var result = await executor.ExecuteActionAsync(action, new FileInfo(zipPath));

        // Assert
        result.Success.Should().BeTrue();
        mockSafeFs.Verify(fs => fs.DeleteFileSafelyAsync(
            zipPath,
            true,
            It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteExtractArchiveAsync_WithPermanentDelete_WithoutAuthorization_FailsClosed()
    {
        // Arrange
        var mockSafeFs = new Mock<ISafeFileSystem>();
        mockSafeFs.Setup(fs => fs.DeleteFileSafelyAsync(It.IsAny<string>(), false, false))
            .ThrowsAsync(new InvalidOperationException("Permanent deletion not authorized."));
        var executor = new ActionExecutor(_variableEngine, safeFileSystem: mockSafeFs.Object);

        var tempDir = _fileHelper.CreateTestDirectory("extract_fail");
        var zipPath = Path.Combine(tempDir.FullName, "fail.zip");
        var contentDir = _fileHelper.CreateTestDirectory("fail_content");
        _fileHelper.CreateTestFile(Path.Combine("fail_content", "sample.txt"), "data");
        System.IO.Compression.ZipFile.CreateFromDirectory(contentDir.FullName, zipPath);

        var targetDest = Path.Combine(tempDir.FullName, "fail_output");
        var action = new ExtractArchiveAction
        {
            DestinationPath = targetDest,
            DeleteAfterExtraction = true,
            UseRecycleBinForArchiveDeletion = false,
            ConfirmBeforeDelete = true
        };

        // Act
        var result = await executor.ExecuteActionAsync(action, new FileInfo(zipPath));

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Permanent deletion not authorized.");
    }

    #endregion

    #region Remove Empty Folders Boundary Clamping Tests

    [Fact]
    public async Task ExecuteMoveAsync_WithRemoveEmptyFolders_StopsAtMonitoredRootBoundary()
    {
        // Arrange: MonitoredRoot / Level1 / Level2 / file.txt
        var monitoredRoot = _fileHelper.CreateTestDirectory("BoundaryMoveRoot");
        var level1 = Directory.CreateDirectory(Path.Combine(monitoredRoot.FullName, "Level1"));
        var level2 = Directory.CreateDirectory(Path.Combine(level1.FullName, "Level2"));
        var sourceFile = _fileHelper.CreateTestFile(Path.Combine("BoundaryMoveRoot", "Level1", "Level2", "sample.txt"), "hello");
        var destDir = _fileHelper.CreateTestDirectory("MoveOutput");

        var action = new MoveFileAction
        {
            DestinationPath = destDir.FullName,
            RemoveEmptyFolders = true,
            MonitoredSourceRoot = monitoredRoot.FullName
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(level2.FullName).Should().BeFalse("Level2 became empty and should be removed");
        Directory.Exists(level1.FullName).Should().BeFalse("Level1 became empty and should be removed");
        Directory.Exists(monitoredRoot.FullName).Should().BeTrue("Monitored root must be clamped and preserved even if empty");
    }

    [Fact]
    public async Task ExecuteDeleteAsync_WithRemoveEmptyFolders_StopsAtMonitoredRootBoundary()
    {
        // Arrange: MonitoredRoot / SubA / SubB / file.txt
        var monitoredRoot = _fileHelper.CreateTestDirectory("BoundaryDeleteRoot");
        var subA = Directory.CreateDirectory(Path.Combine(monitoredRoot.FullName, "SubA"));
        var subB = Directory.CreateDirectory(Path.Combine(subA.FullName, "SubB"));
        var sourceFile = _fileHelper.CreateTestFile(Path.Combine("BoundaryDeleteRoot", "SubA", "SubB", "del.txt"), "data");

        var action = new DeleteFileAction
        {
            UseRecycleBin = false,
            ConfirmBeforeDelete = false,
            RemoveEmptyFolders = true,
            MonitoredSourceRoot = monitoredRoot.FullName
        };

        // Act
        var result = await _executor.ExecuteActionAsync(action, sourceFile);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(subB.FullName).Should().BeFalse("SubB became empty and should be removed");
        Directory.Exists(subA.FullName).Should().BeFalse("SubA became empty and should be removed");
        Directory.Exists(monitoredRoot.FullName).Should().BeTrue("Monitored root must be clamped and preserved even if empty");
    }

    #endregion

}
