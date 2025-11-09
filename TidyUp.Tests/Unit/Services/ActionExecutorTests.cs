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

    public void Dispose()
    {
        _fileHelper.Dispose();
    }
}
