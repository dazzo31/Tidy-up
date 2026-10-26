using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Services.Validation;
using TidyUp.Tests.TestInfrastructure;
using TidyUp.ViewModels;
using TidyUp.ViewModels.RuleEditor;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class RuleWizardModeSynchronizationTests : IDisposable
{
    private readonly IsolatedAppData _appData = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly MainWindowViewModel _mainViewModel;
    private readonly RuleWizardViewModel _wizardViewModel;

    public RuleWizardModeSynchronizationTests()
    {
        _serviceProvider = TestServices.Create(_appData);
        _mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        _wizardViewModel = _serviceProvider.GetRequiredService<RuleWizardViewModel>();
    }

    public void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
        _appData.Dispose();
    }

    [Fact]
    public void RuleWizardViewModel_LoadFromRule_PopulatesAllPropertiesCorrectly()
    {
        var rule = new Rule
        {
            Name = "Organize Invoices",
            Description = "Move PDF invoices to Finance",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = @"C:\Downloads", IncludeSubfolders = true } },
            Conditions = new ConditionGroup { Operator = LogicOperator.Or },
            Actions = { new MoveFileAction { DestinationPath = @"C:\Finance", Order = 1 } }
        };

        _wizardViewModel.LoadFromRule(rule);

        _wizardViewModel.RuleName.Should().Be("Organize Invoices");
        _wizardViewModel.RuleDescription.Should().Be("Move PDF invoices to Finance");
        _wizardViewModel.IsRuleEnabled.Should().BeTrue();
        _wizardViewModel.MonitoredFolders.Should().ContainSingle().Which.Path.Should().Be(@"C:\Downloads");
        _wizardViewModel.ConditionEditor.RootCondition!.Operator.Should().Be(LogicOperator.Or);
        _wizardViewModel.ActionEditor.Actions.Should().ContainSingle();
        _wizardViewModel.CurrentStage.Should().Be(WizardStage.Folders);
    }

    [Fact]
    public void RuleWizardViewModel_SyncToRule_UpdatesRuleEntityWithoutDataLoss()
    {
        var rule = new Rule { Name = "Old Name" };

        _wizardViewModel.RuleName = "New Synchronized Name";
        _wizardViewModel.RuleDescription = "Updated Description";
        _wizardViewModel.IsRuleEnabled = true;
        _wizardViewModel.MonitoredFolders.Clear();
        _wizardViewModel.MonitoredFolders.Add(new MonitoredFolder { Path = @"C:\Work" });
        _wizardViewModel.ConditionEditor.RootCondition = new ConditionGroup { Operator = LogicOperator.And };
        _wizardViewModel.ActionEditor.Actions.Clear();
        _wizardViewModel.ActionEditor.Actions.Add(new MoveFileAction { DestinationPath = @"C:\Archive", Order = 1 });

        _wizardViewModel.SyncToRule(rule);

        rule.Name.Should().Be("New Synchronized Name");
        rule.Description.Should().Be("Updated Description");
        rule.IsEnabled.Should().BeTrue();
        rule.MonitoredFolders.Should().ContainSingle().Which.Path.Should().Be(@"C:\Work");
        rule.Conditions.Should().NotBeNull();
        rule.Conditions!.Operator.Should().Be(LogicOperator.And);
        rule.Actions.Should().ContainSingle().Which.Should().BeOfType<MoveFileAction>();
    }

    [Fact]
    public void MainWindowViewModel_SetEditorMode_SwitchesBetweenGuidedAndAdvancedPreservingState()
    {
        var rule = new Rule
        {
            Name = "Initial Rule",
            IsEnabled = false,
            MonitoredFolders = { new MonitoredFolder { Path = @"C:\Incoming" } }
        };

        _mainViewModel.SelectedRule = rule;
        _mainViewModel.CurrentEditorMode.Should().Be(RuleEditorMode.GuidedWorkflow);

        // Mutate in guided wizard
        _mainViewModel.WizardViewModel.RuleName = "Switched to Advanced";
        _mainViewModel.WizardViewModel.MonitoredFolders.Add(new MonitoredFolder { Path = @"C:\Outgoing" });

        // Switch to Advanced Tabs
        _mainViewModel.SetEditorMode(RuleEditorMode.AdvancedTabs);
        _mainViewModel.CurrentEditorMode.Should().Be(RuleEditorMode.AdvancedTabs);
        _mainViewModel.IsAdvancedModeActive.Should().BeTrue();
        _mainViewModel.IsGuidedModeActive.Should().BeFalse();

        // SelectedRule must have received synchronized data
        rule.Name.Should().Be("Switched to Advanced");
        rule.MonitoredFolders.Should().HaveCount(2);

        // Now mutate in Advanced mode ConditionEditor
        _mainViewModel.ConditionEditor.RootCondition = new ConditionGroup { Operator = LogicOperator.Or };

        // Switch back to Guided mode
        _mainViewModel.SetEditorMode(RuleEditorMode.GuidedWorkflow);
        _mainViewModel.CurrentEditorMode.Should().Be(RuleEditorMode.GuidedWorkflow);
        _mainViewModel.IsGuidedModeActive.Should().BeTrue();

        // WizardViewModel must have received updated conditions
        _mainViewModel.WizardViewModel.ConditionEditor.RootCondition!.Operator.Should().Be(LogicOperator.Or);
    }

    [Fact]
    public void RuleWizardViewModel_ToggleRuleEnabled_UpdatesEnabledSeparatelyFromSave()
    {
        _wizardViewModel.IsRuleEnabled = false;

        _wizardViewModel.ToggleRuleEnabledCommand.Execute(null);
        _wizardViewModel.IsRuleEnabled.Should().BeTrue();

        _wizardViewModel.ToggleRuleEnabledCommand.Execute(null);
        _wizardViewModel.IsRuleEnabled.Should().BeFalse();
    }

    [Fact]
    public void RuleWizardViewModel_ResetWizard_ClearsStateCompletely()
    {
        _wizardViewModel.RuleName = "To Be Cleared";
        _wizardViewModel.MonitoredFolders.Add(new MonitoredFolder { Path = @"C:\Temp" });
        _wizardViewModel.IsRuleEnabled = true;

        _wizardViewModel.ResetWizard();

        _wizardViewModel.RuleName.Should().BeEmpty();
        _wizardViewModel.MonitoredFolders.Should().BeEmpty();
        _wizardViewModel.IsRuleEnabled.Should().BeFalse();
        _wizardViewModel.CurrentStage.Should().Be(WizardStage.Folders);
    }

    [Fact]
    public void MainWindowViewModel_CreateNewRule_DefaultsToDisabledAndGuidedWorkflow()
    {
        _mainViewModel.CreateNewRuleCommand.Execute(null);

        _mainViewModel.SelectedRule.Should().NotBeNull();
        _mainViewModel.SelectedRule!.IsEnabled.Should().BeFalse("newly created rule must default to Disabled");
        _mainViewModel.CurrentEditorMode.Should().Be(RuleEditorMode.GuidedWorkflow);
        _mainViewModel.CurrentView.Should().Be(NavigationView.RuleEditor);
        _mainViewModel.IsRuleDirty.Should().BeTrue();
    }

    [Fact]
    public void RuleWizardViewModel_SavePreservesAllRuleMetadata()
    {
        var existingRule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Preserved Rule",
            Description = "Metadata Description",
            IsEnabled = true,
            ExecutionOrder = 14,
            StopProcessingAfterMatch = true,
            TriggerType = RuleTriggerType.Scheduled,
            ScheduledTime = TimeSpan.FromHours(22),
            CreatedDate = new DateTime(2025, 3, 15, 8, 30, 0, DateTimeKind.Utc),
            LastRunDate = new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            FilesProcessedCount = 137,
            MonitoredFolders = { new MonitoredFolder { Path = @"C:\Monitored" } },
            Actions = { new MoveFileAction { DestinationPath = @"C:\Dest" } }
        };

        _wizardViewModel.LoadFromRule(existingRule);

        // Verify wizard loaded metadata
        _wizardViewModel.ExecutionOrder.Should().Be(14);
        _wizardViewModel.StopProcessingAfterMatch.Should().BeTrue();
        _wizardViewModel.TriggerType.Should().Be(RuleTriggerType.Scheduled);
        _wizardViewModel.ScheduledTime.Should().Be(TimeSpan.FromHours(22));
        _wizardViewModel.CreatedDate.Should().Be(new DateTime(2025, 3, 15, 8, 30, 0, DateTimeKind.Utc));
        _wizardViewModel.LastRunDate.Should().Be(new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc));
        _wizardViewModel.FilesProcessedCount.Should().Be(137);

        // Mutate wizard title and folder
        _wizardViewModel.RuleName = "Updated Name In Wizard";

        // BuildRule must preserve all metadata losslessly
        var builtRule = _wizardViewModel.BuildRule();
        builtRule.Id.Should().Be(existingRule.Id);
        builtRule.Name.Should().Be("Updated Name In Wizard");
        builtRule.ExecutionOrder.Should().Be(14);
        builtRule.StopProcessingAfterMatch.Should().BeTrue();
        builtRule.TriggerType.Should().Be(RuleTriggerType.Scheduled);
        builtRule.ScheduledTime.Should().Be(TimeSpan.FromHours(22));
        builtRule.CreatedDate.Should().Be(new DateTime(2025, 3, 15, 8, 30, 0, DateTimeKind.Utc));
        builtRule.LastRunDate.Should().Be(new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc));
        builtRule.FilesProcessedCount.Should().Be(137);
    }

    [Fact]
    public void RuleWizardViewModel_Cancel_DelegatesToShellHandler()
    {
        bool cancelInvoked = false;
        _wizardViewModel.CancelHandler = () => cancelInvoked = true;

        _wizardViewModel.Cancel();

        cancelInvoked.Should().BeTrue();
    }

    [Fact]
    public void MainWindowViewModel_CancelNewRule_ClearsOrphanedState()
    {
        _mainViewModel.CreateNewRuleCommand.Execute(null);
        _mainViewModel.SelectedRule.Should().NotBeNull();
        _mainViewModel.IsRuleDirty.Should().BeTrue();

        // Mutate draft in wizard
        _mainViewModel.WizardViewModel.RuleName = "Uncommitted New Rule";

        // Confirm discard when cancelling
        _mainViewModel.ConfirmDiscardRuleCallback = (msg, title) => true;
        _mainViewModel.WizardViewModel.CancelCommand.Execute(null);

        // State must be cleanly reset without orphan rules in list
        _mainViewModel.IsRuleDirty.Should().BeFalse();
        _mainViewModel.Rules.Should().NotContain(r => r.Name == "Uncommitted New Rule");
        _mainViewModel.WizardViewModel.RuleName.Should().BeEmpty();
    }

    [Fact]
    public void MainWindowViewModel_DiscardEdits_RestoresNestedConditionsAndActions()
    {
        var rule = new Rule
        {
            Name = "Deep Rule",
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileNameCondition { Value = "initial.txt", Operator = StringOperator.Contains }
                }
            },
            Actions =
            {
                new MoveFileAction { DestinationPath = @"C:\Initial", Order = 1 }
            },
            MonitoredFolders = { new MonitoredFolder { Path = @"C:\Monitored" } }
        };

        _mainViewModel.SelectedRule = rule;
        _mainViewModel.IsRuleDirty.Should().BeFalse();

        // Mutate nested condition and action
        var condition = (FileNameCondition)rule.Conditions!.Conditions[0];
        condition.Value = "mutated.pdf";
        rule.Name = "Mutated Deep Rule";
        _mainViewModel.IsRuleDirty.Should().BeTrue();

        // Discard edits
        _mainViewModel.ConfirmDiscardRuleCallback = (msg, title) => true;
        var proceed = _mainViewModel.ConfirmDiscardRuleEdits();

        proceed.Should().BeTrue();
        _mainViewModel.IsRuleDirty.Should().BeFalse();
        rule.Name.Should().Be("Deep Rule");

        // Verify nested condition was restored to baseline
        var restoredCondition = (FileNameCondition)rule.Conditions!.Conditions[0];
        restoredCondition.Value.Should().Be("initial.txt");
    }
}
