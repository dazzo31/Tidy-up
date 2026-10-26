using FluentAssertions;
using TidyUp.Data.Repositories;
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

    [Fact]
    public void CanReorderRules_Gating_ReflectsFilterSearchAndSortingState()
    {
        _viewModel.SearchText = string.Empty;
        _viewModel.SetFilter(RuleFilterType.All);
        _viewModel.SelectedSortOption = MainWindowViewModel.AvailableSortOptions.First(s => s.Type == RuleSortType.ExecutionPriority);
        _viewModel.CanReorderRules.Should().BeTrue();

        // Search text active
        _viewModel.SearchText = "query";
        _viewModel.CanReorderRules.Should().BeFalse();
        _viewModel.SearchText = string.Empty;
        _viewModel.CanReorderRules.Should().BeTrue();

        // Non-All filter active
        _viewModel.SetFilter(RuleFilterType.EnabledOnly);
        _viewModel.CanReorderRules.Should().BeFalse();
        _viewModel.SetFilter(RuleFilterType.DisabledOnly);
        _viewModel.CanReorderRules.Should().BeFalse();
        _viewModel.SetFilter(RuleFilterType.HasErrors);
        _viewModel.CanReorderRules.Should().BeFalse();
        _viewModel.SetFilter(RuleFilterType.All);
        _viewModel.CanReorderRules.Should().BeTrue();

        // Non-priority sort active
        _viewModel.SelectedSortOption = MainWindowViewModel.AvailableSortOptions.First(s => s.Type == RuleSortType.Name);
        _viewModel.CanReorderRules.Should().BeFalse();
        _viewModel.SelectedSortOption = MainWindowViewModel.AvailableSortOptions.First(s => s.Type == RuleSortType.LastRun);
        _viewModel.CanReorderRules.Should().BeFalse();
        _viewModel.SelectedSortOption = MainWindowViewModel.AvailableSortOptions.First(s => s.Type == RuleSortType.ModificationDate);
        _viewModel.CanReorderRules.Should().BeFalse();
        _viewModel.SelectedSortOption = MainWindowViewModel.AvailableSortOptions.First(s => s.Type == RuleSortType.ExecutionPriority);
        _viewModel.CanReorderRules.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderRulesAsync_WhenCanReorderRulesIsFalse_AbortsAndSurfacesMessage()
    {
        var r1 = new Rule { Name = "R1", ExecutionOrder = 0 };
        var r2 = new Rule { Name = "R2", ExecutionOrder = 1 };
        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(r1);
        _viewModel.Rules.Add(r2);

        _viewModel.SearchText = "R1"; // disables reordering

        var result = await _viewModel.ReorderRulesAsync(r1, r2);

        result.Should().BeFalse();
        _viewModel.StatusMessage.Should().Contain("Reordering is disabled");
        _viewModel.Rules[0].Should().Be(r1);
        _viewModel.Rules[1].Should().Be(r2);
    }

    [Fact]
    public async Task ReorderRulesAsync_WhenValid_PersistsOrdersAtomicallyToRepository()
    {
        var repo = _serviceProvider.GetRequiredService<IRuleRepository>();

        var r1 = new Rule
        {
            Name = "Rule A",
            ExecutionOrder = 0,
            MonitoredFolders = { new MonitoredFolder { Path = @"C:\MonitoredA" } },
            Actions = { new MoveFileAction { DestinationPath = @"C:\DestA" } }
        };
        var r2 = new Rule
        {
            Name = "Rule B",
            ExecutionOrder = 1,
            MonitoredFolders = { new MonitoredFolder { Path = @"C:\MonitoredB" } },
            Actions = { new MoveFileAction { DestinationPath = @"C:\DestB" } }
        };

        await repo.AddRangeAsync(new[] { r1, r2 });

        _viewModel.Rules.Clear();
        _viewModel.Rules.Add(r1);
        _viewModel.Rules.Add(r2);

        // Move Rule A down to position of Rule B
        var result = await _viewModel.ReorderRulesAsync(r1, r2);

        result.Should().BeTrue();
        _viewModel.StatusMessage.Should().Be("Rule execution order updated successfully");

        // In-memory collection should reflect new order and execution priorities
        _viewModel.Rules[0].Id.Should().Be(r2.Id);
        _viewModel.Rules[0].ExecutionOrder.Should().Be(0);
        _viewModel.Rules[1].Id.Should().Be(r1.Id);
        _viewModel.Rules[1].ExecutionOrder.Should().Be(1);

        // Database should be updated atomically
        var persistedRules = await repo.GetAllAsync();
        var persistedR2 = persistedRules.First(r => r.Id == r2.Id);
        var persistedR1 = persistedRules.First(r => r.Id == r1.Id);

        persistedR2.ExecutionOrder.Should().Be(0);
        persistedR1.ExecutionOrder.Should().Be(1);
    }

    [Fact]
    public async Task ReorderRulesAsync_WhenTransactionFails_RestoresPreviousOrderAndSurfacesError()
    {
        var mockRepo = new Moq.Mock<IRuleRepository>();
        mockRepo.Setup(r => r.UpdateExecutionOrdersAsync(Moq.It.IsAny<IEnumerable<(Guid RuleId, int Order)>>()))
            .Throws(new InvalidOperationException("Simulated SQLite write error"));

        var mockImportExport = new Moq.Mock<TidyUp.Services.IImportExportService>();

        var vm = new MainWindowViewModel(
            mockRepo.Object,
            mockImportExport.Object,
            _serviceProvider);

        var r1 = new Rule { Name = "First Rule", ExecutionOrder = 0 };
        var r2 = new Rule { Name = "Second Rule", ExecutionOrder = 1 };
        vm.Rules.Add(r1);
        vm.Rules.Add(r2);

        var result = await vm.ReorderRulesAsync(r1, r2);

        result.Should().BeFalse();
        vm.StatusMessage.Should().Be("Failed to save rule reordering: Simulated SQLite write error");

        // Verification of rollback to original positions and orders
        vm.Rules.Should().HaveCount(2);
        vm.Rules[0].Should().Be(r1);
        vm.Rules[0].ExecutionOrder.Should().Be(0);
        vm.Rules[1].Should().Be(r2);
        vm.Rules[1].ExecutionOrder.Should().Be(1);
    }

    [Fact]
    public async Task ImportRulesFromJsonAsync_WhenValid_PersistsAtomicallyAndEnforcesDisabled()
    {
        string packageJson = """
        {
          "$schema": "https://tidyup.app/schemas/rules-v1.json",
          "version": "1.0",
          "rules": [
            {
              "name": "Imported Alpha",
              "isEnabled": true,
              "monitoredFolders": [{ "path": "C:\\MonitoredAlpha" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\DestAlpha" }]
            },
            {
              "name": "Imported Beta",
              "isEnabled": true,
              "monitoredFolders": [{ "path": "C:\\MonitoredBeta" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\DestBeta" }]
            }
          ]
        }
        """;

        _viewModel.Rules.Clear();
        _viewModel.ConfirmImportCallback = (msg, title) => true;

        var result = await _viewModel.ImportRulesFromJsonAsync(packageJson, "rules.json");

        result.Should().BeTrue();
        _viewModel.StatusMessage.Should().Be("Imported 2 rule(s) from rules.json");
        _viewModel.Rules.Should().HaveCount(2);

        // Invariant: all imported rules MUST be disabled
        _viewModel.Rules[0].Name.Should().Be("Imported Alpha");
        _viewModel.Rules[0].IsEnabled.Should().BeFalse();
        _viewModel.Rules[1].Name.Should().Be("Imported Beta");
        _viewModel.Rules[1].IsEnabled.Should().BeFalse();

        // Database verification
        var repo = _serviceProvider.GetRequiredService<IRuleRepository>();
        var dbRules = await repo.GetAllAsync();
        dbRules.Should().Contain(r => r.Name == "Imported Alpha" && !r.IsEnabled);
        dbRules.Should().Contain(r => r.Name == "Imported Beta" && !r.IsEnabled);
    }

    [Fact]
    public async Task ImportRulesFromJsonAsync_WhenCancelled_ImportsZeroRules()
    {
        string packageJson = """
        {
          "version": "1.0",
          "rules": [
            {
              "name": "Cancelled Rule",
              "monitoredFolders": [{ "path": "C:\\Test" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\Dest" }]
            }
          ]
        }
        """;

        _viewModel.Rules.Clear();
        _viewModel.ConfirmImportCallback = (msg, title) => false; // User cancels dialog

        var result = await _viewModel.ImportRulesFromJsonAsync(packageJson, "rules.json");

        result.Should().BeFalse();
        _viewModel.StatusMessage.Should().Be("Import cancelled");
        _viewModel.Rules.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportRulesFromJsonAsync_WhenValidationFails_AbortsAndSurfacesError()
    {
        string invalidVersionJson = """
        {
          "version": "3.0",
          "rules": [
            {
              "name": "Future Rule",
              "monitoredFolders": [{ "path": "C:\\Test" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\Dest" }]
            }
          ]
        }
        """;

        _viewModel.Rules.Clear();
        _viewModel.ConfirmImportCallback = (msg, title) => true;

        var result = await _viewModel.ImportRulesFromJsonAsync(invalidVersionJson);

        result.Should().BeFalse();
        _viewModel.StatusMessage.Should().Contain("Unsupported schema version '3.0'");
        _viewModel.Rules.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportRulesFromJsonAsync_WhenPersistenceFails_RollsBackZeroRulesAdded()
    {
        var mockRepo = new Moq.Mock<IRuleRepository>();
        mockRepo.Setup(r => r.AddRangeAsync(Moq.It.IsAny<IEnumerable<Rule>>()))
            .Throws(new InvalidOperationException("Simulated SQLite constraint violation"));

        var mockImportExport = new Moq.Mock<TidyUp.Services.IImportExportService>();

        var vm = new MainWindowViewModel(
            mockRepo.Object,
            mockImportExport.Object,
            _serviceProvider)
        {
            ConfirmImportCallback = (msg, title) => true
        };

        string packageJson = """
        {
          "version": "1.0",
          "rules": [
            {
              "name": "Doomed Rule",
              "monitoredFolders": [{ "path": "C:\\Test" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\Dest" }]
            }
          ]
        }
        """;

        var result = await vm.ImportRulesFromJsonAsync(packageJson);

        result.Should().BeFalse();
        vm.StatusMessage.Should().Contain("Simulated SQLite constraint violation");
        vm.Rules.Should().BeEmpty();
    }
}
