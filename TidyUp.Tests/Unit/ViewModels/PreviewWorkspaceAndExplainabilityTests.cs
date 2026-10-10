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

public class PreviewWorkspaceAndExplainabilityTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly Mock<IExecutionPlanGenerator> _planGeneratorMock;
    private readonly Mock<IActionExecutor> _actionExecutorMock;
    private readonly IExplainableDecisionEngine _explainableEngine;

    public PreviewWorkspaceAndExplainabilityTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), $"TidyUp_PreviewTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempTestDir);

        _planGeneratorMock = new Mock<IExecutionPlanGenerator>();
        _actionExecutorMock = new Mock<IActionExecutor>();
        _explainableEngine = new ExplainableDecisionEngine();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void ExplainMatch_WhenFileMatches_ReturnsPassedEvaluation()
    {
        var testFilePath = Path.Combine(_tempTestDir, "report.pdf");
        File.WriteAllText(testFilePath, "test report content");

        var rule = new Rule
        {
            Name = "PDF Rule",
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = ".pdf"
                    }
                }
            }
        };

        var breakdown = _explainableEngine.ExplainMatch(rule, testFilePath);

        breakdown.Should().NotBeNull();
        breakdown.OverallMatch.Should().BeTrue();
        breakdown.ConditionResults.Should().HaveCount(1);
        breakdown.RuleName.Should().Be("PDF Rule");
        breakdown.MatchSummary.Should().Contain("Matched");
    }

    [Fact]
    public void ExplainMatch_WhenFileDoesNotMatch_ReturnsFailedConditionDetails()
    {
        var testFilePath = Path.Combine(_tempTestDir, "photo.jpg");
        File.WriteAllText(testFilePath, "test image");

        var rule = new Rule
        {
            Name = "PDF Only Rule",
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = ".pdf"
                    }
                }
            }
        };

        var breakdown = _explainableEngine.ExplainMatch(rule, testFilePath);

        breakdown.Should().NotBeNull();
        breakdown.OverallMatch.Should().BeFalse();
        breakdown.ConditionResults.Should().HaveCount(1);
        breakdown.ConditionResults[0].Passed.Should().BeFalse();
        breakdown.ConditionResults[0].DetailMessage.Should().Contain(".pdf");
    }

    [Fact]
    public void LoadPlan_ComputesBreakdownCountsAndPopulatesItems()
    {
        var vm = new PreviewViewModel(
            _planGeneratorMock.Object,
            _actionExecutorMock.Object,
            explainableEngine: _explainableEngine);

        var plan = new ExecutionPlan
        {
            TotalScanned = 10,
            TotalMatched = 4,
            PlannedActions =
            {
                new PlannedFileAction { ActionType = ActionType.Move, SourcePath = "C:\\a.txt", TargetPath = "C:\\dest\\a.txt" },
                new PlannedFileAction { ActionType = ActionType.Copy, SourcePath = "C:\\b.txt", TargetPath = "C:\\dest\\b.txt" },
                new PlannedFileAction { ActionType = ActionType.Rename, SourcePath = "C:\\c.txt", TargetPath = "C:\\dest\\c_renamed.txt" },
                new PlannedFileAction { ActionType = ActionType.Delete, SourcePath = "C:\\d.txt", HasConflict = true }
            }
        };

        vm.LoadPlan(plan);

        vm.TotalScannedFiles.Should().Be(10);
        vm.TotalMatchedFiles.Should().Be(4);
        vm.ConflictCount.Should().Be(1);
        vm.MoveCount.Should().Be(1);
        vm.CopyCount.Should().Be(1);
        vm.RenameCount.Should().Be(1);
        vm.DeleteCount.Should().Be(1);
        vm.FilteredItems.Should().HaveCount(4);
    }

    [Fact]
    public void ShowDeletionsOnly_FiltersItemsToDeletions()
    {
        var vm = new PreviewViewModel(
            _planGeneratorMock.Object,
            _actionExecutorMock.Object,
            explainableEngine: _explainableEngine);

        var plan = new ExecutionPlan
        {
            PlannedActions =
            {
                new PlannedFileAction { ActionType = ActionType.Move, SourcePath = "C:\\a.txt" },
                new PlannedFileAction { ActionType = ActionType.Delete, SourcePath = "C:\\b.txt" },
                new PlannedFileAction { ActionType = ActionType.Copy, SourcePath = "C:\\c.txt" }
            }
        };

        vm.LoadPlan(plan);
        vm.FilteredItems.Should().HaveCount(3);

        vm.ShowDeletionsOnly = true;
        vm.FilteredItems.Should().HaveCount(1);
        vm.FilteredItems[0].ActionType.Should().Be(ActionType.Delete);
    }

    [Fact]
    public void PerformPreFlightCheck_WhenFileExistsAndUnmodified_ReturnsValid()
    {
        var testFilePath = Path.Combine(_tempTestDir, "active.txt");
        File.WriteAllText(testFilePath, "active content");
        var fi = new FileInfo(testFilePath);

        var plannedAction = new PlannedFileAction
        {
            SourcePath = testFilePath,
            SourceModifiedDate = fi.LastWriteTime,
            ActionType = ActionType.Move,
            TargetPath = Path.Combine(_tempTestDir, "dest.txt")
        };

        var item = new PlannedActionItemViewModel(plannedAction) { IsApproved = true };

        var vm = new PreviewViewModel(
            _planGeneratorMock.Object,
            _actionExecutorMock.Object,
            explainableEngine: _explainableEngine);

        var (isValid, issues) = vm.PerformPreFlightCheck(new[] { item });

        isValid.Should().BeTrue();
        issues.Should().BeEmpty();
    }

    [Fact]
    public void PerformPreFlightCheck_WhenFileMissingOrModified_ReturnsInvalidWithIssues()
    {
        var missingFilePath = Path.Combine(_tempTestDir, "missing.txt");
        var item1 = new PlannedActionItemViewModel(new PlannedFileAction
        {
            SourcePath = missingFilePath,
            SourceModifiedDate = DateTime.Now,
            ActionType = ActionType.Move
        }) { IsApproved = true };

        var modifiedFilePath = Path.Combine(_tempTestDir, "modified.txt");
        File.WriteAllText(modifiedFilePath, "original content");
        var oldDate = new DateTime(2020, 1, 1);
        var item2 = new PlannedActionItemViewModel(new PlannedFileAction
        {
            SourcePath = modifiedFilePath,
            SourceModifiedDate = oldDate, // Old simulated date vs current disk date
            ActionType = ActionType.Delete
        }) { IsApproved = true };

        var vm = new PreviewViewModel(
            _planGeneratorMock.Object,
            _actionExecutorMock.Object,
            explainableEngine: _explainableEngine);

        var (isValid, issues) = vm.PerformPreFlightCheck(new[] { item1, item2 });

        isValid.Should().BeFalse();
        issues.Should().HaveCount(2);
        issues.Should().Contain(i => i.Contains("no longer exists"));
        issues.Should().Contain(i => i.Contains("modified since preview"));
    }

    [Fact]
    public void ToggleExplainDrawer_FlipsOpenState()
    {
        var vm = new PreviewViewModel(
            _planGeneratorMock.Object,
            _actionExecutorMock.Object,
            explainableEngine: _explainableEngine);

        vm.IsExplainDrawerOpen.Should().BeFalse();
        vm.ToggleExplainDrawer();
        vm.IsExplainDrawerOpen.Should().BeTrue();
        vm.ToggleExplainDrawer();
        vm.IsExplainDrawerOpen.Should().BeFalse();
    }
}
