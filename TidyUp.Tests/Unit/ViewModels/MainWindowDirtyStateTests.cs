using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.State;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class MainWindowDirtyStateTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowDirtyStateTests()
    {
        _serviceProvider = ServiceConfiguration.ConfigureServices();
        _viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
    }

    [Fact]
    public void MainWindowViewModel_InitialState_IsNotDirty()
    {
        _viewModel.IsRuleDirty.Should().BeFalse();
    }

    [Fact]
    public void MainWindowViewModel_SelectingNewRule_ResetsDirtyFlag()
    {
        var rule = new Rule { Name = "Initial Rule" };
        _viewModel.SelectedRule = rule;

        _viewModel.IsRuleDirty.Should().BeFalse();
    }

    [Fact]
    public void MainWindowViewModel_ModifyingRuleName_SetsIsRuleDirty()
    {
        var rule = new Rule { Name = "Test Rule" };
        _viewModel.SelectedRule = rule;
        _viewModel.IsRuleDirty.Should().BeFalse();

        // Mutate rule name
        rule.Name = "Modified Rule Name";

        _viewModel.IsRuleDirty.Should().BeTrue();
    }

    [Fact]
    public void MainWindowViewModel_AddingMonitoredFolder_SetsIsRuleDirty()
    {
        var rule = new Rule { Name = "Folder Rule" };
        _viewModel.SelectedRule = rule;
        _viewModel.IsRuleDirty.Should().BeFalse();

        // Add monitored folder
        rule.MonitoredFolders.Add(new MonitoredFolder { Path = @"C:\TestPath" });

        _viewModel.IsRuleDirty.Should().BeTrue();
    }

    [Fact]
    public void MainWindowViewModel_NavigatingWhenDirty_AbortsWhenConfirmationDeclined()
    {
        var rule = new Rule { Name = "Dirty Rule" };
        _viewModel.SelectedRule = rule;
        rule.Name = "Changed Name";
        _viewModel.IsRuleDirty.Should().BeTrue();

        _viewModel.CurrentView = NavigationView.RuleEditor;

        // User denies discard
        _viewModel.ConfirmDiscardRuleCallback = (msg, title) => false;

        _viewModel.NavigateToDashboardCommand.Execute(null);

        // CurrentView must remain RuleEditor
        _viewModel.CurrentView.Should().Be(NavigationView.RuleEditor);
        _viewModel.IsRuleDirty.Should().BeTrue();
    }

    [Fact]
    public void MainWindowViewModel_NavigatingWhenDirty_ProceedsWhenConfirmationAccepted()
    {
        var rule = new Rule { Name = "Dirty Rule" };
        _viewModel.SelectedRule = rule;
        rule.Name = "Changed Name";
        _viewModel.IsRuleDirty.Should().BeTrue();

        _viewModel.CurrentView = NavigationView.RuleEditor;

        // User confirms discard
        _viewModel.ConfirmDiscardRuleCallback = (msg, title) => true;

        _viewModel.NavigateToDashboardCommand.Execute(null);

        _viewModel.CurrentView.Should().Be(NavigationView.Dashboard);
        _viewModel.IsRuleDirty.Should().BeFalse();
    }

    [Fact]
    public void MainWindowViewModel_SwitchingRuleWhenDirty_AbortsWhenConfirmationDeclined()
    {
        var rule1 = new Rule { Name = "Rule 1" };
        var rule2 = new Rule { Name = "Rule 2" };

        _viewModel.SelectedRule = rule1;
        rule1.Name = "Rule 1 Modified";
        _viewModel.IsRuleDirty.Should().BeTrue();

        // User denies discard
        _viewModel.ConfirmDiscardRuleCallback = (msg, title) => false;

        // Attempt to switch to rule2
        _viewModel.SelectedRule = rule2;

        _viewModel.SelectedRule.Should().Be(rule1);
        _viewModel.IsRuleDirty.Should().BeTrue();
    }

    [Fact]
    public void MainWindowViewModel_SwitchingRuleWhenDirty_ProceedsWhenConfirmationAccepted()
    {
        var rule1 = new Rule { Name = "Rule 1" };
        var rule2 = new Rule { Name = "Rule 2" };

        _viewModel.SelectedRule = rule1;
        rule1.Name = "Rule 1 Modified";
        _viewModel.IsRuleDirty.Should().BeTrue();

        // User confirms discard
        _viewModel.ConfirmDiscardRuleCallback = (msg, title) => true;

        // Attempt to switch to rule2
        _viewModel.SelectedRule = rule2;

        _viewModel.SelectedRule.Should().Be(rule2);
        _viewModel.IsRuleDirty.Should().BeFalse();
    }

    [Fact]
    public void MainWindowViewModel_SavingRule_ResetsDirtyFlag()
    {
        var rule = new Rule { Name = "Rule to Save" };
        _viewModel.SelectedRule = rule;
        rule.Name = "Rule to Save Modified";
        _viewModel.IsRuleDirty.Should().BeTrue();

        _viewModel.SaveRuleCommand.Execute(null);

        _viewModel.IsRuleDirty.Should().BeFalse();
    }
}
