using System.IO;
using FluentAssertions;
using Moq;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class FoldersViewModelTests : IDisposable
{
    private readonly Mock<IRuleRepository> _ruleRepoMock;
    private readonly FoldersViewModel _viewModel;
    private readonly string _tempTestDir;

    public FoldersViewModelTests()
    {
        _ruleRepoMock = new Mock<IRuleRepository>();
        _viewModel = new FoldersViewModel(_ruleRepoMock.Object);

        _tempTestDir = Path.Combine(Path.GetTempPath(), $"TidyUp_FoldersTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempTestDir);
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
    public async Task LoadFoldersAsync_AggregatesFoldersAcrossRules()
    {
        var rule1 = new Rule
        {
            Name = "Rule 1",
            MonitoredFolders = { new MonitoredFolder { Path = _tempTestDir, IncludeSubfolders = true } }
        };

        var rule2 = new Rule
        {
            Name = "Rule 2",
            MonitoredFolders =
            {
                new MonitoredFolder { Path = _tempTestDir, IncludeSubfolders = true },
                new MonitoredFolder { Path = @"C:\NonExistent_TidyUp_TestFolder", IncludeSubfolders = false }
            }
        };

        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule> { rule1, rule2 });

        await _viewModel.LoadFoldersAsync();

        _viewModel.MonitoredFolders.Should().HaveCount(2);
        _viewModel.TotalFoldersCount.Should().Be(2);

        var tempItem = _viewModel.MonitoredFolders.FirstOrDefault(f => f.Path.Equals(_tempTestDir, StringComparison.OrdinalIgnoreCase));
        tempItem.Should().NotBeNull();
        tempItem!.LinkedRulesCount.Should().Be(2);
        tempItem.LinkedRulesSummary.Should().Contain("Rule 1");
        tempItem.LinkedRulesSummary.Should().Contain("Rule 2");
        tempItem.Exists.Should().BeTrue();
        tempItem.IsHealthy.Should().BeTrue();

        var missingItem = _viewModel.MonitoredFolders.FirstOrDefault(f => f.Path.Contains("NonExistent"));
        missingItem.Should().NotBeNull();
        missingItem!.Exists.Should().BeFalse();
        missingItem.StatusBadgeText.Should().Be("Missing");
    }

    [Fact]
    public void InspectPath_DetectsSystemProtectedPaths()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var item = new MonitoredFolderItemViewModel { Path = winDir };

        FoldersViewModel.InspectPath(item);

        item.IsSystemProtected.Should().BeTrue();
        item.StatusBadgeText.Should().Be("Protected System Path");
    }

    [Fact]
    public void AddMonitoredFolder_ParsesExclusionPatternsAndAvoidsDuplicates()
    {
        _viewModel.NewFolderPathInput = _tempTestDir;
        _viewModel.NewFolderIncludeSubfolders = true;
        _viewModel.NewFolderExclusionsInput = "*.tmp, node_modules, .git";

        _viewModel.AddMonitoredFolderCommand.Execute(null);

        _viewModel.MonitoredFolders.Should().ContainSingle();
        var added = _viewModel.MonitoredFolders[0];
        added.Path.Should().Be(_tempTestDir);
        added.ExclusionPatterns.Should().BeEquivalentTo(new[] { "*.tmp", "node_modules", ".git" });

        // Attempt adding duplicate
        _viewModel.NewFolderPathInput = _tempTestDir;
        _viewModel.AddMonitoredFolderCommand.Execute(null);

        _viewModel.MonitoredFolders.Should().HaveCount(1);
        _viewModel.StatusMessage.Should().Contain("already being monitored");
    }

    [Fact]
    public void SearchText_FiltersAggregatedFoldersDynamically()
    {
        var item1 = new MonitoredFolderItemViewModel { Path = @"C:\Downloads\Invoices", LinkedRulesSummary = "Invoices Rule" };
        var item2 = new MonitoredFolderItemViewModel { Path = @"C:\Work\Receipts", LinkedRulesSummary = "Receipts Rule" };

        _viewModel.MonitoredFolders.Add(item1);
        _viewModel.MonitoredFolders.Add(item2);

        _viewModel.SearchText = "Invoices";
        _viewModel.FilteredFolders.Should().ContainSingle().Which.Path.Should().Contain("Invoices");

        _viewModel.SearchText = "Receipts";
        _viewModel.FilteredFolders.Should().ContainSingle().Which.Path.Should().Contain("Receipts");

        _viewModel.SearchText = string.Empty;
        _viewModel.FilteredFolders.Should().HaveCount(2);
    }

    [Fact]
    public void TogglePauseWatcher_TogglesStateAndStatus()
    {
        var item = new MonitoredFolderItemViewModel { Path = _tempTestDir, Exists = true, HasReadPermission = true, HasWritePermission = true };
        item.WatcherStatus = "Watching";
        item.IsPaused = false;

        _viewModel.TogglePauseWatcherCommand.Execute(item);
        item.IsPaused.Should().BeTrue();
        item.WatcherStatus.Should().Be("Paused");

        _viewModel.TogglePauseWatcherCommand.Execute(item);
        item.IsPaused.Should().BeFalse();
        item.WatcherStatus.Should().Be("Watching");
    }

    [Fact]
    public async Task ScanFolderNowAsync_CountsFilesAccurately()
    {
        File.WriteAllText(Path.Combine(_tempTestDir, "test1.txt"), "hello");
        File.WriteAllText(Path.Combine(_tempTestDir, "test2.log"), "world");

        var item = new MonitoredFolderItemViewModel
        {
            Path = _tempTestDir,
            IncludeSubfolders = true
        };

        await _viewModel.ScanFolderNowAsync(item);

        _viewModel.StatusMessage.Should().Contain("Scan complete: found 2 active files");
    }
}
