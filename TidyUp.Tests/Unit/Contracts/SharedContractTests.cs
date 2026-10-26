using FluentAssertions;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Rollback;
using Xunit;

namespace TidyUp.Tests.Unit.Contracts;

public class SharedContractTests
{
    [Fact]
    public void ExecutionAuthorizationContext_ValidatesActionAndPathAuthorization()
    {
        var actionId1 = Guid.NewGuid();
        var actionId2 = Guid.NewGuid();
        var deletePath = @"C:\Users\Test\DeleteMe.txt";
        var overwritePath = @"C:\Users\Test\Target.txt";

        var auth = new ExecutionAuthorizationContext
        {
            PlanId = Guid.NewGuid(),
            RuleId = Guid.NewGuid(),
            AuthorizedActionIds = { actionId1 },
            AuthorizedPermanentDeletionPaths = { deletePath },
            AuthorizedOverwritePaths = { overwritePath }
        };

        // Action authorization
        auth.IsActionAuthorized(actionId1).Should().BeTrue();
        auth.IsActionAuthorized(actionId2).Should().BeFalse();

        // Path authorization with case-insensitivity on Windows
        auth.IsPermanentDeleteAuthorized(@"c:\users\test\deleteme.txt").Should().BeTrue();
        auth.IsPermanentDeleteAuthorized(@"C:\Other\File.txt").Should().BeFalse();

        auth.IsOverwriteAuthorized(@"c:\users\test\target.txt").Should().BeTrue();
        auth.IsOverwriteAuthorized(@"C:\Other\File.txt").Should().BeFalse();
    }

    [Fact]
    public void ActionContinuationResolver_ResolvesCorrectSourcePathForEachActionType()
    {
        var originalSource = @"C:\Input\file.txt";
        var movedTarget = @"C:\Output\file.txt";
        var renamedTarget = @"C:\Input\renamed.txt";
        var copiedTarget = @"C:\Backup\file.txt";

        // Move
        var moveAction = new MoveFileAction { DestinationPath = @"C:\Output" };
        ActionContinuationResolver.ComputeNextSourcePath(moveAction, originalSource, movedTarget)
            .Should().Be(movedTarget);

        // Rename
        var renameAction = new RenameFileAction { NamePattern = "renamed" };
        ActionContinuationResolver.ComputeNextSourcePath(renameAction, originalSource, renamedTarget)
            .Should().Be(renamedTarget);

        // Copy - ApplyToSourceFile = true
        var copyApplySource = new CopyFileAction { DestinationPath = @"C:\Backup", ApplyToSourceFile = true };
        ActionContinuationResolver.ComputeNextSourcePath(copyApplySource, originalSource, copiedTarget)
            .Should().Be(originalSource);

        // Copy - ApplyToSourceFile = false
        var copyApplyTarget = new CopyFileAction { DestinationPath = @"C:\Backup", ApplyToSourceFile = false };
        ActionContinuationResolver.ComputeNextSourcePath(copyApplyTarget, originalSource, copiedTarget)
            .Should().Be(copiedTarget);

        // Delete terminates chain
        var deleteAction = new DeleteFileAction();
        ActionContinuationResolver.ComputeNextSourcePath(deleteAction, originalSource, null)
            .Should().BeNull();

        // ExtractArchive produces directory, not file
        var extractAction = new ExtractArchiveAction();
        ActionContinuationResolver.ComputeNextSourcePath(extractAction, originalSource, @"C:\Extracted")
            .Should().BeNull();

        // RunCommand produces command text, not file
        var runAction = new RunCommandAction();
        ActionContinuationResolver.ComputeNextSourcePath(runAction, originalSource, "echo hello")
            .Should().BeNull();
    }

    [Fact]
    public void RuleDraft_MaintainsIsolatedWorkingCopyAndTracksNestedChanges()
    {
        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Original Rule",
            Description = "Base description",
            IsEnabled = false,
            ExecutionOrder = 1,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileNameCondition { Value = "report", Operator = StringOperator.Contains }
                }
            },
            Actions =
            {
                new MoveFileAction { DestinationPath = @"C:\PDFs", Order = 1 }
            }
        };

        var draft = new RuleDraft(rule);

        // Initial state: not dirty
        draft.IsDirty.Should().BeFalse();
        draft.WorkingCopy.Name.Should().Be("Original Rule");

        // Mutate top-level property
        draft.WorkingCopy.Name = "Modified Rule";
        draft.IsDirty.Should().BeTrue();
        draft.BaselineSnapshot.Name.Should().Be("Original Rule", "Baseline must remain unmutated");

        // Reset to baseline
        draft.ResetToBaseline();
        draft.IsDirty.Should().BeFalse();
        draft.WorkingCopy.Name.Should().Be("Original Rule");

        // Mutate nested condition property
        var condition = (FileNameCondition)draft.WorkingCopy.Conditions!.Conditions[0];
        condition.Value = "invoice";
        draft.IsDirty.Should().BeTrue("Nested condition edits must trigger dirty detection");

        // Mutate nested action property
        draft.ResetToBaseline();
        draft.IsDirty.Should().BeFalse();
        var action = (MoveFileAction)draft.WorkingCopy.Actions[0];
        action.DestinationPath = @"C:\Documents";
        draft.IsDirty.Should().BeTrue("Nested action edits must trigger dirty detection");

        // Runtime counters alone do NOT make draft dirty
        draft.ResetToBaseline();
        draft.WorkingCopy.FilesProcessedCount = 999;
        draft.WorkingCopy.LastRunDate = DateTime.UtcNow;
        draft.IsDirty.Should().BeFalse("Runtime execution statistics must not mark draft dirty");
    }

    [Fact]
    public void RollbackOutcome_ProvidesDistinctFailureAndGuidanceStates()
    {
        var result = new RollbackOperationResult
        {
            OperationId = Guid.NewGuid(),
            BatchId = Guid.NewGuid(),
            Success = false,
            Outcome = RollbackOutcome.ManualRestoreRequired,
            RestorationInstructions = "Open Windows Recycle Bin to restore this file."
        };

        result.Outcome.Should().Be(RollbackOutcome.ManualRestoreRequired);
        result.Success.Should().BeFalse();
        result.RestorationInstructions.Should().NotBeNullOrEmpty();
    }
}
