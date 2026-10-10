using FluentAssertions;
using Moq;
using TidyUp.Data.Entities;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Diagnostics;
using TidyUp.Services.Simulation;
using TidyUp.ViewModels;

namespace TidyUp.Tests.Unit.Services;

public class ExecutionDiscrepancyAnalyzerTests
{
    private readonly ExecutionDiscrepancyAnalyzer _analyzer;

    public ExecutionDiscrepancyAnalyzerTests()
    {
        _analyzer = new ExecutionDiscrepancyAnalyzer();
    }

    [Fact]
    public void Analyze_WhenAllActionsSucceedAsPlanned_ZeroDiscrepancies()
    {
        // Arrange
        var action1 = new PlannedFileAction
        {
            SourcePath = @"C:\Source\doc1.pdf",
            TargetPath = @"C:\Dest\doc1.pdf",
            ActionType = ActionType.Move,
            FileSizeBytes = 1024,
            SourceModifiedDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        var journal1 = new OperationJournalEntry
        {
            OriginalPath = @"C:\Source\doc1.pdf",
            TargetPath = @"C:\Dest\doc1.pdf",
            ActionType = "Move",
            Status = "Completed"
        };

        // Act
        var report = _analyzer.Analyze(new[] { action1 }, new[] { journal1 });

        // Assert
        report.TotalPlanned.Should().Be(1);
        report.ExecutedAsPlannedCount.Should().Be(1);
        report.DiscrepancyCount.Should().Be(0);
        report.HasAnyDiscrepancies.Should().BeFalse();
        report.Items[0].Outcome.Should().Be(DiscrepancyOutcome.ExecutedAsPlanned);
        report.SummaryText.Should().Contain("0 discrepancies");
    }

    [Fact]
    public void Analyze_WhenSourceFileDisappeared_CategorizesAsFileDisappeared()
    {
        // Arrange: Custom file provider reporting file does not exist
        var analyzer = new ExecutionDiscrepancyAnalyzer(path => (Exists: false, LastWriteTime: DateTime.MinValue, Size: 0));

        var action = new PlannedFileAction
        {
            SourcePath = @"C:\Source\vanished.txt",
            TargetPath = @"C:\Dest\vanished.txt",
            ActionType = ActionType.Move
        };

        // Act: No journal entry recorded because file wasn't found to run
        var report = analyzer.Analyze(new[] { action }, Enumerable.Empty<OperationJournalEntry>());

        // Assert
        report.DiscrepancyCount.Should().Be(1);
        report.Items[0].Outcome.Should().Be(DiscrepancyOutcome.FileDisappeared);
        report.Items[0].Explanation.Should().Contain("deleted or moved before execution");
    }

    [Fact]
    public void Analyze_WhenFileLockedByExternalProcess_CategorizesAsSkippedDueToLock()
    {
        // Arrange
        var action = new PlannedFileAction
        {
            SourcePath = @"C:\Source\locked_file.xlsx",
            TargetPath = @"C:\Dest\locked_file.xlsx",
            ActionType = ActionType.Move
        };

        var journal = new OperationJournalEntry
        {
            OriginalPath = @"C:\Source\locked_file.xlsx",
            Status = "Failed",
            Details = "The process cannot access the file because it is being used by another process. (Sharing violation)"
        };

        // Act
        var report = _analyzer.Analyze(new[] { action }, new[] { journal });

        // Assert
        report.DiscrepancyCount.Should().Be(1);
        report.Items[0].Outcome.Should().Be(DiscrepancyOutcome.SkippedDueToLock);
        report.Items[0].Explanation.Should().Contain("Sharing violation");
    }

    [Fact]
    public void Analyze_WhenPermissionDenied_CategorizesAsFailedDueToPermission()
    {
        // Arrange
        var action = new PlannedFileAction
        {
            SourcePath = @"C:\System\secure.log",
            TargetPath = @"C:\Dest\secure.log",
            ActionType = ActionType.Move
        };

        var journal = new OperationJournalEntry
        {
            OriginalPath = @"C:\System\secure.log",
            Status = "Failed",
            Details = "Access is denied. UnauthorizedAccessException"
        };

        // Act
        var report = _analyzer.Analyze(new[] { action }, new[] { journal });

        // Assert
        report.DiscrepancyCount.Should().Be(1);
        report.Items[0].Outcome.Should().Be(DiscrepancyOutcome.FailedDueToPermission);
        report.Items[0].Explanation.Should().Contain("Access is denied");
    }

    [Fact]
    public void Analyze_WhenFileModifiedSincePreview_CategorizesAsModifiedSincePreview()
    {
        // Arrange: Custom file provider where file timestamp differs by 1 hour from preview
        var previewTime = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var actualTime = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc);

        var analyzer = new ExecutionDiscrepancyAnalyzer(path => (Exists: true, LastWriteTime: actualTime, Size: 2048));

        var action = new PlannedFileAction
        {
            SourcePath = @"C:\Source\edited_doc.txt",
            TargetPath = @"C:\Dest\edited_doc.txt",
            ActionType = ActionType.Copy,
            SourceModifiedDate = previewTime,
            FileSizeBytes = 1024
        };

        var result = new ActionResult
        {
            Success = true,
            ResultPath = @"C:\Dest\edited_doc.txt"
        };

        // Act
        var report = analyzer.AnalyzeFromResults(new[] { action }, new[] { (action, result) });

        // Assert
        report.DiscrepancyCount.Should().Be(1);
        report.Items[0].Outcome.Should().Be(DiscrepancyOutcome.ModifiedSincePreview);
        report.Items[0].Explanation.Should().Contain("timestamp changed");
    }

    [Fact]
    public void Analyze_WhenActionNotExecuted_CategorizesAsNotExecuted()
    {
        // Arrange: File exists on disk, but was not executed
        var analyzer = new ExecutionDiscrepancyAnalyzer(path => (Exists: true, LastWriteTime: DateTime.UtcNow, Size: 100));

        var action = new PlannedFileAction
        {
            SourcePath = @"C:\Source\unapproved.pdf",
            TargetPath = @"C:\Dest\unapproved.pdf",
            ActionType = ActionType.Move
        };

        // Act
        var report = analyzer.Analyze(new[] { action }, Enumerable.Empty<OperationJournalEntry>());

        // Assert
        report.DiscrepancyCount.Should().Be(1);
        report.Items[0].Outcome.Should().Be(DiscrepancyOutcome.NotExecuted);
        report.Items[0].Explanation.Should().Contain("not executed");
    }

    [Fact]
    public void ExecutionSummaryViewModel_ShowDiscrepanciesOnly_FiltersCorrectly()
    {
        // Arrange
        var report = new ExecutionDiscrepancyReport
        {
            TotalPlanned = 2,
            ExecutedAsPlannedCount = 1,
            DiscrepancyCount = 1,
            Items = new List<ActionDiscrepancyItem>
            {
                new() { SourcePath = "file1.txt", Outcome = DiscrepancyOutcome.ExecutedAsPlanned },
                new() { SourcePath = "file2.txt", Outcome = DiscrepancyOutcome.SkippedDueToLock }
            }
        };

        // Act: Loading report with discrepancies defaults ShowDiscrepanciesOnly to true
        var vm = new ExecutionSummaryViewModel(report);

        // Assert: Filtered to only discrepancies
        vm.ShowDiscrepanciesOnly.Should().BeTrue();
        vm.DisplayItems.Should().HaveCount(1);
        vm.DisplayItems[0].SourcePath.Should().Be("file2.txt");

        // Act: Toggle off
        vm.ShowDiscrepanciesOnly = false;
        vm.DisplayItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task PreviewViewModel_ApplyChangesAsync_TriggersDiscrepancyReportWhenFailureOccurs()
    {
        // Arrange
        var mockPlanGen = new Mock<IExecutionPlanGenerator>();
        var mockExecutor = new Mock<IActionExecutor>();

        var plannedAction = new PlannedFileAction
        {
            SourcePath = @"C:\NonExistent\ghost.txt",
            TargetPath = @"C:\Dest\ghost.txt",
            ActionType = ActionType.Move,
            FileAction = new MoveFileAction { DestinationPath = @"C:\Dest" },
            HasConflict = false
        };

        var plan = new ExecutionPlan
        {
            TotalScanned = 1,
            TotalMatched = 1,
            PlannedActions = { plannedAction }
        };

        ExecutionDiscrepancyReport? capturedReport = null;

        var analyzer = new ExecutionDiscrepancyAnalyzer();
        var vm = new PreviewViewModel(mockPlanGen.Object, mockExecutor.Object, analyzer)
        {
            ConfirmApplyHandler = (msg, title) => true,
            ShowDiscrepancySummaryHandler = report => capturedReport = report
        };

        vm.LoadPlan(plan);
        vm.AllItems[0].IsApproved = true;

        // Act: Source file doesn't exist on disk, so it will fail with "Source file not found"
        await vm.ApplyChangesAsync();

        // Assert: Discrepancy report generated and captured
        vm.DiscrepancyReport.Should().NotBeNull();
        vm.DiscrepancyReport!.HasAnyDiscrepancies.Should().BeTrue();
        capturedReport.Should().NotBeNull();
        capturedReport!.DiscrepancyCount.Should().Be(1);
        capturedReport.Items[0].Outcome.Should().Be(DiscrepancyOutcome.FileDisappeared);
    }
}
