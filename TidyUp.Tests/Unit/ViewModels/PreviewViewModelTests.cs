using System.Collections.ObjectModel;
using System.IO;
using FluentAssertions;
using Moq;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Simulation;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class PreviewViewModelTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly Mock<IExecutionPlanGenerator> _planGeneratorMock;
    private readonly Mock<IActionExecutor> _actionExecutorMock;
    private readonly PreviewViewModel _viewModel;

    public PreviewViewModelTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "TidyUp_PreviewVmTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);

        _planGeneratorMock = new Mock<IExecutionPlanGenerator>();
        _actionExecutorMock = new Mock<IActionExecutor>();

        _viewModel = new PreviewViewModel(_planGeneratorMock.Object, _actionExecutorMock.Object)
        {
            // Default confirmation handler to true for testing
            ConfirmApplyHandler = (_, _) => true
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private ExecutionPlan CreateSampleExecutionPlan()
    {
        var plan = new ExecutionPlan
        {
            RuleName = "Test Invoices Rule",
            TotalScanned = 10,
            TotalMatched = 3,
            PlannedActions = new List<PlannedFileAction>
            {
                new PlannedFileAction
                {
                    SourcePath = Path.Combine(_testTempDir, "invoice1.pdf"),
                    TargetPath = Path.Combine(_testTempDir, "Archive", "invoice1.pdf"),
                    ActionType = ActionType.Move,
                    FileAction = new MoveFileAction { DestinationPath = Path.Combine(_testTempDir, "Archive") },
                    RuleName = "Test Invoices Rule",
                    HasConflict = false
                },
                new PlannedFileAction
                {
                    SourcePath = Path.Combine(_testTempDir, "receipt2.jpg"),
                    TargetPath = Path.Combine(_testTempDir, "Images", "receipt2.jpg"),
                    ActionType = ActionType.Copy,
                    FileAction = new CopyFileAction { DestinationPath = Path.Combine(_testTempDir, "Images") },
                    RuleName = "Receipts Rule",
                    HasConflict = false
                },
                new PlannedFileAction
                {
                    SourcePath = Path.Combine(_testTempDir, "temp_cache.tmp"),
                    TargetPath = null,
                    ActionType = ActionType.Delete,
                    FileAction = new DeleteFileAction { UseRecycleBin = true },
                    RuleName = "Cleanup Rule",
                    HasConflict = true,
                    ConflictDescription = "File is locked by background process"
                }
            }
        };

        return plan;
    }

    #region Loading & Statistics Tests

    [Fact]
    public void LoadPlan_PopulatesAllItemsAndFilterOptions_CalculatesCounts()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();

        // Act
        _viewModel.LoadPlan(plan);

        // Assert
        _viewModel.AllItems.Should().HaveCount(3);
        _viewModel.FilteredItems.Should().HaveCount(3);
        _viewModel.TotalScannedFiles.Should().Be(10);
        _viewModel.TotalMatchedFiles.Should().Be(3);
        _viewModel.ConflictCount.Should().Be(1);
        _viewModel.ApprovedCount.Should().Be(2); // Only unconflicted items default to approved
        _viewModel.RuleFilterOptions.Should().Contain(new[] { "All Rules", "Test Invoices Rule", "Receipts Rule", "Cleanup Rule" });
        _viewModel.ExtensionFilterOptions.Should().Contain(new[] { "All Extensions", ".pdf", ".jpg", ".tmp" });
    }

    #endregion

    #region Filtering Tests

    [Fact]
    public void Filter_ByRule_FiltersItemsCorrectly()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();
        _viewModel.LoadPlan(plan);

        // Act
        _viewModel.SelectedRuleFilter = "Test Invoices Rule";

        // Assert
        _viewModel.FilteredItems.Should().HaveCount(1);
        _viewModel.FilteredItems[0].SourcePath.Should().EndWith("invoice1.pdf");
    }

    [Fact]
    public void Filter_ByActionType_FiltersItemsCorrectly()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();
        _viewModel.LoadPlan(plan);

        // Act
        _viewModel.SelectedActionTypeFilter = "Copy";

        // Assert
        _viewModel.FilteredItems.Should().HaveCount(1);
        _viewModel.FilteredItems[0].ActionType.Should().Be(ActionType.Copy);
    }

    [Fact]
    public void Filter_ByExtension_FiltersItemsCorrectly()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();
        _viewModel.LoadPlan(plan);

        // Act
        _viewModel.SelectedExtensionFilter = ".pdf";

        // Assert
        _viewModel.FilteredItems.Should().HaveCount(1);
        _viewModel.FilteredItems[0].Extension.Should().Be(".pdf");
    }

    [Fact]
    public void Filter_ShowConflictsOnly_NarrowsToConflictedItems()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();
        _viewModel.LoadPlan(plan);

        // Act
        _viewModel.ShowConflictsOnly = true;

        // Assert
        _viewModel.FilteredItems.Should().HaveCount(1);
        _viewModel.FilteredItems[0].HasConflict.Should().BeTrue();
        _viewModel.FilteredItems[0].ConflictDescription.Should().Contain("File is locked");
    }

    [Fact]
    public void Filter_SearchText_FiltersMatchingPaths()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();
        _viewModel.LoadPlan(plan);

        // Act
        _viewModel.SearchText = "receipt";

        // Assert
        _viewModel.FilteredItems.Should().HaveCount(1);
        _viewModel.FilteredItems[0].SourcePath.Should().Contain("receipt2");
    }

    #endregion

    #region Conflict Prevention & Execution State Tests

    [Fact]
    public void CanApplyChanges_Disabled_WhenAnyApprovedItemHasConflict()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();
        _viewModel.LoadPlan(plan);

        // Conflicted item is unapproved by default, so CanApplyChanges is true
        _viewModel.CanApplyChanges.Should().BeTrue();

        // Act: User approves conflicted item
        var conflictedItem = _viewModel.AllItems.First(i => i.HasConflict);
        conflictedItem.IsApproved = true;

        // Assert: CanApplyChanges must now be false
        _viewModel.CanApplyChanges.Should().BeFalse();

        // Act: User unapproves conflicted item
        conflictedItem.IsApproved = false;

        // Assert: CanApplyChanges should become true again
        _viewModel.CanApplyChanges.Should().BeTrue();
    }

    [Fact]
    public void CanApplyChanges_Disabled_WhenNoItemsApproved()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();
        _viewModel.LoadPlan(plan);

        // Act: Deselect all
        _viewModel.SetAllApproved(false);

        // Assert
        _viewModel.CanApplyChanges.Should().BeFalse();
    }

    [Fact]
    public void CanApplyChanges_Disabled_WhenPlanIsNull()
    {
        // Assert
        _viewModel.ExecutionPlan.Should().BeNull();
        _viewModel.CanApplyChanges.Should().BeFalse();
    }

    #endregion

    #region Apply Changes & Progress Tests

    [Fact]
    public async Task ApplyChangesAsync_CancelledByUser_DoesNotExecuteActions()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();
        _viewModel.LoadPlan(plan);
        _viewModel.ConfirmApplyHandler = (_, _) => false; // User cancels confirmation

        // Act
        await _viewModel.ApplyChangesAsync();

        // Assert
        _actionExecutorMock.Verify(
            x => x.ExecuteActionAsync(It.IsAny<FileAction>(), It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()),
            Times.Never);
        _viewModel.StatusMessage.Should().Contain("cancelled");
        _viewModel.ExecutedSuccessCount.Should().Be(0);
    }

    [Fact]
    public async Task ApplyChangesAsync_ConfirmedByUser_ExecutesOnlyApprovedActions()
    {
        // Arrange
        var file1 = Path.Combine(_testTempDir, "doc1.txt");
        var file2 = Path.Combine(_testTempDir, "doc2.txt");
        File.WriteAllText(file1, "Content 1");
        File.WriteAllText(file2, "Content 2");

        var moveAction1 = new MoveFileAction { DestinationPath = Path.Combine(_testTempDir, "Dest") };
        var moveAction2 = new MoveFileAction { DestinationPath = Path.Combine(_testTempDir, "Dest") };

        var plan = new ExecutionPlan
        {
            PlannedActions =
            {
                new PlannedFileAction
                {
                    SourcePath = file1,
                    TargetPath = Path.Combine(_testTempDir, "Dest", "doc1.txt"),
                    ActionType = ActionType.Move,
                    FileAction = moveAction1,
                    HasConflict = false
                },
                new PlannedFileAction
                {
                    SourcePath = file2,
                    TargetPath = Path.Combine(_testTempDir, "Dest", "doc2.txt"),
                    ActionType = ActionType.Move,
                    FileAction = moveAction2,
                    HasConflict = false
                }
            }
        };

        _viewModel.LoadPlan(plan);

        // Approve item 0, unapprove item 1
        _viewModel.AllItems[0].IsApproved = true;
        _viewModel.AllItems[1].IsApproved = false;

        _actionExecutorMock
            .Setup(x => x.ExecuteActionAsync(moveAction1, It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new ActionResult { Success = true, ResultPath = Path.Combine(_testTempDir, "Dest", "doc1.txt") });

        // Act
        await _viewModel.ApplyChangesAsync();

        // Assert
        _actionExecutorMock.Verify(
            x => x.ExecuteActionAsync(moveAction1, It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()),
            Times.Once);

        _actionExecutorMock.Verify(
            x => x.ExecuteActionAsync(moveAction2, It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()),
            Times.Never);

        _viewModel.ExecutedSuccessCount.Should().Be(1);
        _viewModel.ExecutedFailureCount.Should().Be(0);
        _viewModel.AllItems[0].IsExecuted.Should().BeTrue();
        _viewModel.AllItems[0].ExecutionFailed.Should().BeFalse();
        _viewModel.AllItems[0].ExecutionStatus.Should().Be("Completed");
    }

    [Fact]
    public async Task SimulateRuleAsync_CallsPlanGenerator_AndLoadsPlan()
    {
        // Arrange
        var rule = new Rule { Name = "Simulated Rule" };
        var generatedPlan = CreateSampleExecutionPlan();

        _planGeneratorMock
            .Setup(x => x.GeneratePlanAsync(rule, It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedPlan);

        // Act
        await _viewModel.SimulateRuleAsync(rule);

        // Assert
        _viewModel.ExecutionPlan.Should().BeSameAs(generatedPlan);
        _viewModel.AllItems.Should().HaveCount(3);
        _planGeneratorMock.Verify(x => x.GeneratePlanAsync(rule, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyChangesAsync_DestructiveSafeguardCancelled_AbortsWithZeroFilesModified()
    {
        // Arrange: permanent delete action
        var testFile = Path.Combine(_testTempDir, "to_delete.log");
        File.WriteAllText(testFile, "Log contents");

        var deleteAction = new DeleteFileAction { UseRecycleBin = false }; // Permanent deletion risk!
        var plan = new ExecutionPlan
        {
            PlannedActions =
            {
                new PlannedFileAction
                {
                    SourcePath = testFile,
                    ActionType = ActionType.Delete,
                    FileAction = deleteAction,
                    HasConflict = false
                }
            }
        };

        _viewModel.LoadPlan(plan);
        _viewModel.AllItems[0].IsApproved = true;

        bool safeguardInvoked = false;
        _viewModel.SafeguardDialogHandler = vm =>
        {
            safeguardInvoked = true;
            vm.HasPermanentDeletionRisk.Should().BeTrue();
            return false; // User hits Cancel or Esc
        };

        // Act
        await _viewModel.ApplyChangesAsync();

        // Assert
        safeguardInvoked.Should().BeTrue();
        _actionExecutorMock.Verify(
            x => x.ExecuteActionAsync(It.IsAny<FileAction>(), It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()),
            Times.Never);
        _viewModel.StatusMessage.Should().Contain("Safeguard check cancelled");
        _viewModel.ExecutedSuccessCount.Should().Be(0);
        File.Exists(testFile).Should().BeTrue(); // zero files modified!
    }

    [Fact]
    public async Task ApplyChangesAsync_DestructiveSafeguardConfirmed_ExecutesActions()
    {
        // Arrange: permanent delete action
        var testFile = Path.Combine(_testTempDir, "confirmed_delete.log");
        File.WriteAllText(testFile, "Log contents");

        var deleteAction = new DeleteFileAction { UseRecycleBin = false };
        var plan = new ExecutionPlan
        {
            PlannedActions =
            {
                new PlannedFileAction
                {
                    SourcePath = testFile,
                    ActionType = ActionType.Delete,
                    FileAction = deleteAction,
                    HasConflict = false
                }
            }
        };

        _viewModel.LoadPlan(plan);
        _viewModel.AllItems[0].IsApproved = true;

        bool safeguardInvoked = false;
        _viewModel.SafeguardDialogHandler = vm =>
        {
            safeguardInvoked = true;
            return true; // User explicitly confirms risks
        };

        _actionExecutorMock
            .Setup(x => x.ExecuteActionAsync(deleteAction, It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new ActionResult { Success = true });

        // Act
        await _viewModel.ApplyChangesAsync();

        // Assert
        safeguardInvoked.Should().BeTrue();
        _actionExecutorMock.Verify(
            x => x.ExecuteActionAsync(deleteAction, It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()),
            Times.Once);
        _viewModel.ExecutedSuccessCount.Should().Be(1);
    }

    #endregion
    #region Incomplete Scan Warnings & Partial Plan Tests

    [Fact]
    public void LoadPlan_WithIncompletePlan_ExposesScanWarnings()
    {
        // Arrange
        var plan = new ExecutionPlan
        {
            IsIncomplete = true,
            ScanErrors = new List<string> { "Access denied to folder X" }
        };

        // Act
        _viewModel.LoadPlan(plan);

        // Assert
        _viewModel.HasScanWarnings.Should().BeTrue();
        _viewModel.ScanWarningMessage.Should().Contain("partially completed");
        _viewModel.StatusMessage.Should().Contain("partially completed");
    }

    [Fact]
    public async Task ApplyChangesAsync_WithIncompletePlan_DecliningConfirmation_AbortsApply()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var moveAction = new MoveFileAction
        {
            DestinationPath = Path.GetTempPath(),
            ConflictResolution = ConflictResolution.Skip
        };

        var plan = new ExecutionPlan
        {
            IsIncomplete = true,
            ScanErrors = new List<string> { "Inaccessible subfolder" },
            PlannedActions = new List<PlannedFileAction>
            {
                new PlannedFileAction
                {
                    SourcePath = tempFile,
                    TargetPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(tempFile)),
                    FileAction = moveAction,
                    HasConflict = false
                }
            }
        };

        _viewModel.LoadPlan(plan);
        _viewModel.AllItems[0].IsApproved = true;

        bool partialWarningPrompted = false;
        _viewModel.ConfirmApplyHandler = (msg, title) =>
        {
            if (title == "Partial Scan Warning")
            {
                partialWarningPrompted = true;
                return false; // User declines proceeding with partial scan
            }
            return true;
        };

        // Act
        await _viewModel.ApplyChangesAsync();

        // Assert
        partialWarningPrompted.Should().BeTrue();
        _viewModel.StatusMessage.Should().Contain("partial scan acknowledgment declined");
        _viewModel.ExecutedSuccessCount.Should().Be(0);

        try { File.Delete(tempFile); } catch { }
    }

    #endregion
    #region ClearPlan & Refresh Command Tests

    [Fact]
    public void ClearPlan_ResetsAllItemsAndPlanState()
    {
        // Arrange
        var plan = new ExecutionPlan
        {
            RuleName = "Test Rule",
            PlannedActions = new List<PlannedFileAction>
            {
                new PlannedFileAction
                {
                    SourcePath = "file1.txt",
                    ActionType = ActionType.Move,
                    HasConflict = false
                }
            }
        };

        _viewModel.LoadPlan(plan);
        _viewModel.ExecutionPlan.Should().NotBeNull();
        _viewModel.AllItems.Should().HaveCount(1);
        _viewModel.MoveCount.Should().Be(1);

        // Act
        _viewModel.ClearPlan();

        // Assert
        _viewModel.ExecutionPlan.Should().BeNull();
        _viewModel.AllItems.Should().BeEmpty();
        _viewModel.MoveCount.Should().Be(0);
        _viewModel.CanApplyChanges.Should().BeFalse();
        _viewModel.StatusMessage.Should().Contain("No execution plan generated");
    }

    [Fact]
    public async Task RefreshPreviewAsync_WithActiveRule_ReRunsSimulation()
    {
        // Arrange
        var rule = new Rule { Name = "Refreshable Rule" };
        var freshPlan = new ExecutionPlan
        {
            RuleName = "Refreshable Rule",
            PlannedActions = new List<PlannedFileAction>()
        };

        _planGeneratorMock
            .Setup(g => g.GeneratePlanAsync(rule, It.IsAny<CancellationToken>()))
            .ReturnsAsync(freshPlan);

        _viewModel.CurrentRule = rule;

        // Act
        await _viewModel.RefreshPreviewCommand.ExecuteAsync(null);

        // Assert
        _planGeneratorMock.Verify(g => g.GeneratePlanAsync(rule, It.IsAny<CancellationToken>()), Times.Once);
        _viewModel.ExecutionPlan.Should().BeSameAs(freshPlan);
    }

    [Fact]
    public void PlanHeaderInfo_WhenPlanLoaded_ContainsRuleNameAndTimestamp()
    {
        // Arrange
        var plan = new ExecutionPlan
        {
            RuleName = "HeaderRule",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _viewModel.LoadPlan(plan);

        // Assert
        _viewModel.PlanHeaderInfo.Should().Contain("HeaderRule");
        _viewModel.PlanHeaderInfo.Should().Contain("generated at");
    }

    #endregion

    #region TASK-REM-21 (TU-24): Post-Execution Selection & Apply Button Eligibility Predicate Tests

    [Fact]
    public async Task ApplyChangesAsync_DisablesApplyButton_AndUpdatesCaption_WhenAllApprovedActionsSucceed()
    {
        // Arrange
        var file1 = Path.Combine(_testTempDir, "succ1.txt");
        var file2 = Path.Combine(_testTempDir, "succ2.txt");
        File.WriteAllText(file1, "Content 1");
        File.WriteAllText(file2, "Content 2");

        var moveAction1 = new MoveFileAction { DestinationPath = Path.Combine(_testTempDir, "Dest") };
        var moveAction2 = new MoveFileAction { DestinationPath = Path.Combine(_testTempDir, "Dest") };

        var plan = new ExecutionPlan
        {
            PlannedActions =
            {
                new PlannedFileAction
                {
                    SourcePath = file1,
                    TargetPath = Path.Combine(_testTempDir, "Dest", "succ1.txt"),
                    ActionType = ActionType.Move,
                    FileAction = moveAction1,
                    HasConflict = false
                },
                new PlannedFileAction
                {
                    SourcePath = file2,
                    TargetPath = Path.Combine(_testTempDir, "Dest", "succ2.txt"),
                    ActionType = ActionType.Move,
                    FileAction = moveAction2,
                    HasConflict = false
                }
            }
        };

        _viewModel.LoadPlan(plan);
        _viewModel.PendingApprovedCount.Should().Be(2);
        _viewModel.CanApplyChanges.Should().BeTrue();
        _viewModel.ApplyButtonText.Should().Be("Apply 2 approved changes");

        _actionExecutorMock
            .Setup(x => x.ExecuteActionAsync(It.IsAny<FileAction>(), It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new ActionResult { Success = true, ResultPath = Path.Combine(_testTempDir, "Dest", "succ.txt") });

        // Act
        await _viewModel.ApplyChangesAsync();

        // Assert: Apply button is immediately disabled and caption reflects terminal success
        _viewModel.PendingApprovedCount.Should().Be(0);
        _viewModel.CanApplyChanges.Should().BeFalse();
        _viewModel.ApplyChangesCommand.CanExecute(null).Should().BeFalse();
        _viewModel.ApplyButtonText.Should().Be("All approved changes applied");
        _viewModel.ExecutedSuccessCount.Should().Be(2);
        _viewModel.ExecutedFailureCount.Should().Be(0);
    }

    [Fact]
    public async Task ApplyChangesAsync_DisablesApplyButton_AndShowsFailureCaption_WhenSomeActionsFail()
    {
        // Arrange
        var file1 = Path.Combine(_testTempDir, "fail1.txt");
        var file2 = Path.Combine(_testTempDir, "fail2.txt");
        File.WriteAllText(file1, "Content 1");
        File.WriteAllText(file2, "Content 2");

        var moveAction1 = new MoveFileAction { DestinationPath = Path.Combine(_testTempDir, "Dest") };
        var moveAction2 = new MoveFileAction { DestinationPath = Path.Combine(_testTempDir, "Dest") };

        var plan = new ExecutionPlan
        {
            PlannedActions =
            {
                new PlannedFileAction
                {
                    SourcePath = file1,
                    TargetPath = Path.Combine(_testTempDir, "Dest", "fail1.txt"),
                    ActionType = ActionType.Move,
                    FileAction = moveAction1,
                    HasConflict = false
                },
                new PlannedFileAction
                {
                    SourcePath = file2,
                    TargetPath = Path.Combine(_testTempDir, "Dest", "fail2.txt"),
                    ActionType = ActionType.Move,
                    FileAction = moveAction2,
                    HasConflict = false
                }
            }
        };

        _viewModel.LoadPlan(plan);

        _actionExecutorMock
            .Setup(x => x.ExecuteActionAsync(moveAction1, It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new ActionResult { Success = true, ResultPath = Path.Combine(_testTempDir, "Dest", "fail1.txt") });
        _actionExecutorMock
            .Setup(x => x.ExecuteActionAsync(moveAction2, It.IsAny<FileInfo>(), It.IsAny<int?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new ActionResult { Success = false, ErrorMessage = "Access Denied" });

        // Act
        await _viewModel.ApplyChangesAsync();

        // Assert: Apply button is immediately disabled, failure is reported, and completed actions cannot be re-applied
        _viewModel.PendingApprovedCount.Should().Be(0);
        _viewModel.CanApplyChanges.Should().BeFalse();
        _viewModel.ApplyChangesCommand.CanExecute(null).Should().BeFalse();
        _viewModel.ApplyButtonText.Should().Be("Execution completed with failures");
        _viewModel.ExecutedSuccessCount.Should().Be(1);
        _viewModel.ExecutedFailureCount.Should().Be(1);
        _viewModel.StatusMessage.Should().Contain("completed with errors: 1 succeeded, 1 failed");
    }

    [Fact]
    public void ExecutedActions_CannotBeReApprovedOrReapplied()
    {
        // Arrange
        var plan = CreateSampleExecutionPlan();
        _viewModel.LoadPlan(plan);

        var firstItem = _viewModel.AllItems.First(i => !i.HasConflict);
        firstItem.IsExecuted = true;

        // Assert
        firstItem.CanToggleApproval.Should().BeFalse();
        firstItem.CanBeApplied.Should().BeFalse();

        // Calling SetAllApproved(true) must not re-approve executed items
        _viewModel.SetAllApproved(true);
        _viewModel.AllItems.Where(i => i.IsExecuted).Should().OnlyContain(i => !i.CanBeApplied);
    }

    #endregion
}
