using FluentAssertions;
using Moq;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Simulation;
using TidyUp.Services.Validation;
using TidyUp.ViewModels.RuleEditor;

namespace TidyUp.Tests.Unit.ViewModels;

public class RuleWizardViewModelTests
{
    private readonly Mock<IRuleRepository> _ruleRepoMock = new();
    private readonly Mock<IRuleValidator> _ruleValidatorMock = new();
    private readonly Mock<IExecutionPlanGenerator> _planGeneratorMock = new();

    private RuleWizardViewModel CreateViewModel()
    {
        return new RuleWizardViewModel(
            _ruleRepoMock.Object,
            _ruleValidatorMock.Object,
            _planGeneratorMock.Object);
    }

    [Fact]
    public void InitialState_StartsAtStage1Folders_WithPreviousDisabled()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        vm.CurrentStage.Should().Be(WizardStage.Folders);
        vm.CanGoPrevious.Should().BeFalse();
        vm.IsReviewStage.Should().BeFalse();
        vm.StageTitle.Should().Contain("Step 1");
    }

    [Fact]
    public void Stage1_Validation_BlocksAdvance_WhenNameOrFoldersMissing()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act 1: Missing Name
        vm.NextStage();
        vm.CurrentStage.Should().Be(WizardStage.Folders);
        vm.ErrorMessage.Should().Contain("name");

        // Act 2: Has Name but 0 folders
        vm.RuleName = "Auto Clean Downloads";
        vm.NextStage();
        vm.CurrentStage.Should().Be(WizardStage.Folders);
        vm.ErrorMessage.Should().Contain("folder");

        // Act 3: Has Name and folder -> advances to Conditions
        vm.AddFolder(@"C:\Users\Test\Downloads");
        vm.NextStage();
        vm.CurrentStage.Should().Be(WizardStage.Conditions);
        vm.ErrorMessage.Should().BeNull();
        vm.CanGoPrevious.Should().BeTrue();
    }

    [Fact]
    public void AddFolder_PreventsDuplicates_AndAllowsRemoval()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act 1: Add folder
        vm.AddFolder(@"C:\Test\Folder");
        vm.MonitoredFolders.Should().HaveCount(1);

        // Act 2: Add duplicate
        vm.AddFolder(@"C:\Test\Folder");
        vm.MonitoredFolders.Should().HaveCount(1);
        vm.ErrorMessage.Should().Contain("already");

        // Act 3: Remove folder
        var item = vm.MonitoredFolders[0];
        vm.RemoveFolder(item);
        vm.MonitoredFolders.Should().BeEmpty();
    }

    [Fact]
    public void Stage3_Validation_BlocksAdvance_WhenActionsEmpty()
    {
        // Arrange: Navigate to Actions stage
        var vm = CreateViewModel();
        vm.RuleName = "Test Rule";
        vm.AddFolder(@"C:\Test\Folder");
        vm.NextStage(); // -> Conditions
        vm.NextStage(); // -> Actions

        vm.CurrentStage.Should().Be(WizardStage.Actions);

        // Act: Try advancing with no actions
        vm.NextStage();

        // Assert: Blocked
        vm.CurrentStage.Should().Be(WizardStage.Actions);
        vm.ErrorMessage.Should().Contain("action");

        // Act: Add action -> advances to Review
        vm.ActionEditor.Actions.Add(new MoveFileAction
        {
            DestinationPath = @"C:\Archive",
            ConflictResolution = ConflictResolution.Overwrite
        });
        _ruleValidatorMock.Setup(v => v.ValidateRule(It.IsAny<Rule>()))
            .Returns(RuleValidationResult.Success());

        vm.NextStage();

        // Assert: At Review stage
        vm.CurrentStage.Should().Be(WizardStage.Review);
        vm.IsReviewStage.Should().BeTrue();
    }

    [Fact]
    public void BuildRule_EnforcesDisabledStateByDefault()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RuleName = "Invoices Organizer";
        vm.RuleDescription = "Organizes monthly invoices";
        vm.AddFolder(@"C:\Invoices");
        vm.ActionEditor.Actions.Add(new MoveFileAction { DestinationPath = @"C:\Archive\Invoices" });

        // Act
        var rule = vm.BuildRule();

        // Assert: Explicit requirement from TASK-GUI-02
        rule.IsEnabled.Should().BeFalse("newly created rules must start in Disabled state by default to protect user files");
        rule.Name.Should().Be("Invoices Organizer");
        rule.Description.Should().Be("Organizes monthly invoices");
        rule.MonitoredFolders.Should().HaveCount(1);
        rule.Actions.Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveRuleAsync_WhenValid_SavesToRepositoryInDisabledState_AndFiresRuleSaved()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RuleName = "Clean Screenshots";
        vm.AddFolder(@"C:\Screenshots");
        vm.ActionEditor.Actions.Add(new DeleteFileAction { UseRecycleBin = true });

        _ruleValidatorMock.Setup(v => v.ValidateRule(It.IsAny<Rule>()))
            .Returns(RuleValidationResult.Success());

        Rule? savedRuleEvent = null;
        bool closeRequested = false;
        vm.RuleSaved += (s, r) => savedRuleEvent = r;
        vm.RequestClose += () => closeRequested = true;

        // Act
        await vm.SaveRuleAsync();

        // Assert
        _ruleRepoMock.Verify(r => r.AddAsync(It.Is<Rule>(rule =>
            rule.Name == "Clean Screenshots" &&
            rule.IsEnabled == false)), Times.Once);

        savedRuleEvent.Should().NotBeNull();
        savedRuleEvent!.IsEnabled.Should().BeFalse();
        closeRequested.Should().BeTrue();
    }

    [Fact]
    public async Task SaveRuleAsync_WhenValidationFails_SetsErrorMessage_AndDoesNotSave()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RuleName = "Bad Cyclic Rule";
        vm.AddFolder(@"C:\FolderA");

        var invalidResult = new RuleValidationResult
        {
            Errors = new List<RuleValidationError>
            {
                new RuleValidationError
                {
                    ErrorCode = RuleValidationErrorCode.DestinationIsSubfolderOfMonitoredSource,
                    ErrorMessage = "Destination folder is inside the monitored folder."
                }
            }
        };
        _ruleValidatorMock.Setup(v => v.ValidateRule(It.IsAny<Rule>())).Returns(invalidResult);

        // Act
        await vm.SaveRuleAsync();

        // Assert
        _ruleRepoMock.Verify(r => r.AddAsync(It.IsAny<Rule>()), Times.Never);
        vm.ErrorMessage.Should().Contain("inside the monitored folder");
    }

    [Fact]
    public async Task RunDryRunAsync_SimulatesRuleWithoutErrors()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RuleName = "Dry Run Test";
        vm.AddFolder(@"C:\Test");

        var mockPlan = new ExecutionPlan
        {
            RuleName = "Dry Run Test",
            TotalScanned = 25,
            TotalMatched = 5
        };
        _planGeneratorMock.Setup(p => p.GeneratePlanAsync(It.IsAny<Rule>(), default))
            .ReturnsAsync(mockPlan);

        // Act
        await vm.RunDryRunAsync();

        // Assert
        vm.ExecutionPlan.Should().NotBeNull();
        vm.ExecutionPlan!.TotalScanned.Should().Be(25);
        vm.ExecutionPlan.TotalMatched.Should().Be(5);
    }
}

