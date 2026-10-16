using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Rules;
using TidyUp.Services.Validation;
using TidyUp.Tests.TestInfrastructure;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class MainWindowRulesListTests : IDisposable
{
    private readonly IsolatedAppData _appData = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowRulesListTests()
    {
        _serviceProvider = TestServices.Create(_appData);
        _viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
    }

    public void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
        _appData.Dispose();
    }

    [Fact]
    public void Rule_DefaultState_IsDisabled()
    {
        var rule = new Rule();
        rule.IsEnabled.Should().BeFalse("newly created rules must default to disabled per safe-by-default specification");
    }

    [Fact]
    public void MainWindowViewModel_FilteringByAll_ReturnsAllRules()
    {
        var r1 = new Rule { Name = "Rule 1", IsEnabled = true };
        var r2 = new Rule { Name = "Rule 2", IsEnabled = false };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(r1);
        _viewModel.Rules.Add(r2);

        _viewModel.SetFilter(RuleFilterType.All);

        _viewModel.FilteredRules.Should().HaveCount(2);
        _viewModel.FilteredRules.Should().Contain(r1);
        _viewModel.FilteredRules.Should().Contain(r2);
    }

    [Fact]
    public void MainWindowViewModel_FilteringByEnabled_ReturnsOnlyEnabledRules()
    {
        var r1 = new Rule { Name = "Rule 1", IsEnabled = true };
        var r2 = new Rule { Name = "Rule 2", IsEnabled = false };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(r1);
        _viewModel.Rules.Add(r2);

        _viewModel.SetFilter(RuleFilterType.EnabledOnly);

        _viewModel.FilteredRules.Should().ContainSingle();
        _viewModel.FilteredRules.Should().Contain(r1);
        _viewModel.FilteredRules.Should().NotContain(r2);
    }

    [Fact]
    public void MainWindowViewModel_FilteringByDisabled_ReturnsOnlyDisabledRules()
    {
        var r1 = new Rule { Name = "Rule 1", IsEnabled = true };
        var r2 = new Rule { Name = "Rule 2", IsEnabled = false };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(r1);
        _viewModel.Rules.Add(r2);

        _viewModel.SetFilter(RuleFilterType.DisabledOnly);

        _viewModel.FilteredRules.Should().ContainSingle();
        _viewModel.FilteredRules.Should().Contain(r2);
        _viewModel.FilteredRules.Should().NotContain(r1);
    }

    [Fact]
    public void MainWindowViewModel_FilteringByErrors_ReturnsOnlyRulesWithErrors()
    {
        var rValid = new Rule { Name = "Valid Rule", IsEnabled = true };
        var rError = new Rule { Name = "Broken Rule", IsEnabled = true, HasErrors = true, ErrorMessage = "Missing folder" };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(rValid);
        _viewModel.Rules.Add(rError);

        _viewModel.SetFilter(RuleFilterType.HasErrors);

        _viewModel.FilteredRules.Should().ContainSingle();
        _viewModel.FilteredRules.Should().Contain(rError);
        _viewModel.FilteredRules.Should().NotContain(rValid);
    }

    [Fact]
    public void MainWindowViewModel_SortingByName_OrdersAlphabetically()
    {
        var rB = new Rule { Name = "Beta", ExecutionOrder = 1 };
        var rA = new Rule { Name = "Alpha", ExecutionOrder = 2 };
        var rC = new Rule { Name = "Charlie", ExecutionOrder = 0 };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(rB);
        _viewModel.Rules.Add(rA);
        _viewModel.Rules.Add(rC);

        _viewModel.SelectedSortOption = MainWindowViewModel.AvailableSortOptions.First(s => s.Type == RuleSortType.Name);

        _viewModel.FilteredRules.Select(r => r.Name).Should().ContainInOrder("Alpha", "Beta", "Charlie");
    }

    [Fact]
    public void MainWindowViewModel_SortingByPriority_OrdersByExecutionOrderAscending()
    {
        var r3 = new Rule { Name = "Rule 3", ExecutionOrder = 3 };
        var r1 = new Rule { Name = "Rule 1", ExecutionOrder = 1 };
        var r2 = new Rule { Name = "Rule 2", ExecutionOrder = 2 };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(r3);
        _viewModel.Rules.Add(r1);
        _viewModel.Rules.Add(r2);

        _viewModel.SelectedSortOption = MainWindowViewModel.AvailableSortOptions.First(s => s.Type == RuleSortType.ExecutionPriority);

        _viewModel.FilteredRules.Select(r => r.ExecutionOrder).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void MainWindowViewModel_SortingByLastRun_OrdersDescending()
    {
        var now = DateTime.UtcNow;
        var rOld = new Rule { Name = "Old", LastRunDate = now.AddHours(-5) };
        var rNew = new Rule { Name = "New", LastRunDate = now.AddMinutes(-5) };
        var rNever = new Rule { Name = "Never", LastRunDate = null };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(rOld);
        _viewModel.Rules.Add(rNever);
        _viewModel.Rules.Add(rNew);

        _viewModel.SelectedSortOption = MainWindowViewModel.AvailableSortOptions.First(s => s.Type == RuleSortType.LastRun);

        _viewModel.FilteredRules.Select(r => r.Name).Should().ContainInOrder("New", "Old", "Never");
    }

    [Fact]
    public void MainWindowViewModel_SortingByModified_OrdersDescending()
    {
        var now = DateTime.UtcNow;
        var rOld = new Rule { Name = "Old", ModifiedDate = now.AddHours(-10) };
        var rNew = new Rule { Name = "New", ModifiedDate = now.AddMinutes(-1) };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(rOld);
        _viewModel.Rules.Add(rNew);

        _viewModel.SelectedSortOption = MainWindowViewModel.AvailableSortOptions.First(s => s.Type == RuleSortType.ModificationDate);

        _viewModel.FilteredRules.Select(r => r.Name).Should().ContainInOrder("New", "Old");
    }

    [Fact]
    public void MainWindowViewModel_SearchText_FiltersMatchingNameOrSummary()
    {
        var r1 = new Rule { Name = "Invoice Sorter", Summary = "Move invoices to Accounting" };
        var r2 = new Rule { Name = "Photo Cleaner", Summary = "Delete duplicate pictures" };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(r1);
        _viewModel.Rules.Add(r2);

        _viewModel.SearchText = "invoices";
        _viewModel.FilteredRules.Should().ContainSingle().Which.Name.Should().Be("Invoice Sorter");

        _viewModel.SearchText = "Cleaner";
        _viewModel.FilteredRules.Should().ContainSingle().Which.Name.Should().Be("Photo Cleaner");
    }

    [Fact]
    public void MainWindowViewModel_UpdateRuleMetadata_PopulatesSummaryAndValidationState()
    {
        var rule = new Rule
        {
            Name = "Valid Test Rule",
            Conditions = new ConditionGroup { Operator = LogicOperator.And },
            MonitoredFolders = { new MonitoredFolder { Path = @"C:\MonitoredTest" } },
            Actions = { new MoveFileAction { DestinationPath = @"C:\DestinationTest" } }
        };

        _viewModel.UpdateRuleMetadata(rule);

        rule.Summary.Should().NotBeNullOrWhiteSpace();
        rule.HasErrors.Should().BeFalse();
        rule.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MainWindowViewModel_ModifyingRule_SetsRuleIsDirtyAndViewModelIsDirty()
    {
        var rule = new Rule { Name = "Test Dirty Rule" };
        _viewModel.SelectedRule = rule;
        _viewModel.IsRuleDirty.Should().BeFalse();
        rule.IsDirty.Should().BeFalse();

        // Mutate rule name
        rule.Name = "Test Dirty Rule (Renamed)";

        _viewModel.IsRuleDirty.Should().BeTrue();
        rule.IsDirty.Should().BeTrue();
    }
}
