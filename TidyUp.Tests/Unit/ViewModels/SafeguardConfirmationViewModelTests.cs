using FluentAssertions;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.ViewModels;
using TidyUp.ViewModels.Dialogs;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class SafeguardConfirmationViewModelTests
{
    private readonly SafeguardConfirmationViewModel _viewModel;

    public SafeguardConfirmationViewModelTests()
    {
        _viewModel = new SafeguardConfirmationViewModel(batchThreshold: 50);
    }

    #region Risk Detection Tests

    [Fact]
    public void EvaluateRisks_PermanentDeletion_DetectsRiskAndPopulatesFilesAtRisk()
    {
        // Arrange: DeleteFileAction with UseRecycleBin = false
        var permanentDeleteAction = new PlannedFileAction
        {
            SourcePath = @"C:\Test\secret.doc",
            ActionType = ActionType.Delete,
            FileAction = new DeleteFileAction { UseRecycleBin = false },
            RuleName = "Shredder Rule"
        };

        var items = new List<PlannedActionItemViewModel>
        {
            new PlannedActionItemViewModel(permanentDeleteAction) { IsApproved = true }
        };

        // Act
        var result = _viewModel.EvaluateRisks(items);

        // Assert
        result.Should().BeTrue();
        _viewModel.HasPermanentDeletionRisk.Should().BeTrue();
        _viewModel.PermanentDeletionCount.Should().Be(1);
        _viewModel.RiskSummary.Should().Contain("Permanent Deletion");
        _viewModel.FilesAtRisk.Should().HaveCount(1);
        _viewModel.FilesAtRisk[0].RiskType.Should().Be(FileRiskType.PermanentDeletion);
        _viewModel.FilesAtRisk[0].RiskBadgeText.Should().Be("PERMANENT DELETE");
        _viewModel.FilesAtRisk[0].FilePath.Should().Be(@"C:\Test\secret.doc");
    }

    [Fact]
    public void EvaluateRisks_RecycleBinDeletion_DoesNotTriggerPermanentDeletionRisk()
    {
        // Arrange: DeleteFileAction with UseRecycleBin = true (safe deletion)
        var recycleAction = new PlannedFileAction
        {
            SourcePath = @"C:\Test\temp.tmp",
            ActionType = ActionType.Delete,
            FileAction = new DeleteFileAction { UseRecycleBin = true },
            RuleName = "Recycle Rule"
        };

        var items = new List<PlannedActionItemViewModel>
        {
            new PlannedActionItemViewModel(recycleAction) { IsApproved = true }
        };

        // Act
        var result = _viewModel.EvaluateRisks(items);

        // Assert
        result.Should().BeFalse();
        _viewModel.HasPermanentDeletionRisk.Should().BeFalse();
        _viewModel.FilesAtRisk.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRisks_FileOverwrite_DetectsRiskAndPopulatesFilesAtRisk()
    {
        // Arrange: MoveFileAction with Overwrite conflict resolution
        var overwriteAction = new PlannedFileAction
        {
            SourcePath = @"C:\Source\data.csv",
            TargetPath = @"C:\Target\data.csv",
            ActionType = ActionType.Move,
            ConflictResolution = ConflictResolution.Overwrite,
            FileAction = new MoveFileAction { DestinationPath = @"C:\Target", ConflictResolution = ConflictResolution.Overwrite },
            RuleName = "Data Sync Rule"
        };

        var items = new List<PlannedActionItemViewModel>
        {
            new PlannedActionItemViewModel(overwriteAction) { IsApproved = true }
        };

        // Act
        var result = _viewModel.EvaluateRisks(items);

        // Assert
        result.Should().BeTrue();
        _viewModel.HasOverwriteRisk.Should().BeTrue();
        _viewModel.OverwriteCount.Should().Be(1);
        _viewModel.RiskSummary.Should().Contain("File Overwrite");
        _viewModel.FilesAtRisk.Should().HaveCount(1);
        _viewModel.FilesAtRisk[0].RiskType.Should().Be(FileRiskType.FileOverwrite);
        _viewModel.FilesAtRisk[0].RiskBadgeText.Should().Be("OVERWRITE");
    }

    [Fact]
    public void EvaluateRisks_LargeBatchThreshold_DetectsWhenExceedingThreshold()
    {
        // Arrange: 55 non-destructive move items exceeding threshold of 50
        var items = new List<PlannedActionItemViewModel>();
        for (int i = 0; i < 55; i++)
        {
            var action = new PlannedFileAction
            {
                SourcePath = $@"C:\Source\file_{i}.txt",
                TargetPath = $@"C:\Target\file_{i}.txt",
                ActionType = ActionType.Move,
                ConflictResolution = ConflictResolution.Skip,
                FileAction = new MoveFileAction { DestinationPath = @"C:\Target" },
                RuleName = "Bulk Rule"
            };
            items.Add(new PlannedActionItemViewModel(action) { IsApproved = true });
        }

        // Act
        var result = _viewModel.EvaluateRisks(items);

        // Assert
        result.Should().BeTrue();
        _viewModel.HasLargeBatchRisk.Should().BeTrue();
        _viewModel.TotalBatchCount.Should().Be(55);
        _viewModel.RiskSummary.Should().Contain("High-Volume Operation: Batch contains 55 files");
        _viewModel.FilesAtRisk.Should().NotBeEmpty();
    }

    [Fact]
    public void EvaluateRisks_NonDestructiveSmallBatch_ReturnsFalseWithoutRisks()
    {
        // Arrange: 5 simple moves with Skip
        var items = new List<PlannedActionItemViewModel>();
        for (int i = 0; i < 5; i++)
        {
            var action = new PlannedFileAction
            {
                SourcePath = $@"C:\Source\doc_{i}.pdf",
                TargetPath = $@"C:\Target\doc_{i}.pdf",
                ActionType = ActionType.Move,
                ConflictResolution = ConflictResolution.Skip,
                FileAction = new MoveFileAction { DestinationPath = @"C:\Target" },
                RuleName = "Standard Move Rule"
            };
            items.Add(new PlannedActionItemViewModel(action) { IsApproved = true });
        }

        // Act
        var result = _viewModel.EvaluateRisks(items);

        // Assert
        result.Should().BeFalse();
        _viewModel.HasPermanentDeletionRisk.Should().BeFalse();
        _viewModel.HasOverwriteRisk.Should().BeFalse();
        _viewModel.HasLargeBatchRisk.Should().BeFalse();
        _viewModel.FilesAtRisk.Should().BeEmpty();
    }

    #endregion

    #region Command & Callback Tests

    [Fact]
    public void ConfirmCommand_SetsIsConfirmedTrue_AndCallsRequestClose()
    {
        // Arrange
        bool closeCalled = false;
        _viewModel.RequestClose = () => closeCalled = true;

        // Act
        _viewModel.ConfirmCommand.Execute(null);

        // Assert
        _viewModel.IsConfirmed.Should().BeTrue();
        closeCalled.Should().BeTrue();
    }

    [Fact]
    public void CancelCommand_SetsIsConfirmedFalse_AndCallsRequestClose()
    {
        // Arrange
        bool closeCalled = false;
        _viewModel.RequestClose = () => closeCalled = true;

        // Act
        _viewModel.CancelCommand.Execute(null);

        // Assert
        _viewModel.IsConfirmed.Should().BeFalse();
        closeCalled.Should().BeTrue();
    }

    #endregion
}

