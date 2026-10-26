using TidyUp.Models.Folders;
using TidyUp.Services.Folders;
using TidyUp.Services;
using System.Collections.ObjectModel;
using System.IO;
using TidyUp.Services.Watcher;
using FluentAssertions;
using Moq;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
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
        missingItem.Status.Should().Be(FolderStatus.Missing);
    }

    [Fact]
    public void InspectPath_DetectsSystemProtectedPaths()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var item = new MonitoredFolderItemViewModel { Path = winDir };

        FoldersViewModel.InspectPath(item);

        item.IsSystemProtected.Should().BeTrue();
        item.Status.Should().Be(FolderStatus.Protected);
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

    [Fact]
    public void AddMonitoredFolderCommand_CanExecute_ReflectsNewFolderPathInputPresence()
    {
        // Initially empty -> disabled
        _viewModel.NewFolderPathInput = string.Empty;
        _viewModel.AddMonitoredFolderCommand.CanExecute(null).Should().BeFalse();

        // Whitespace only -> disabled
        _viewModel.NewFolderPathInput = "   ";
        _viewModel.AddMonitoredFolderCommand.CanExecute(null).Should().BeFalse();

        // Path entered manually -> enabled
        _viewModel.NewFolderPathInput = _tempTestDir;
        _viewModel.AddMonitoredFolderCommand.CanExecute(null).Should().BeTrue();

        // Executing adds folder, clears input, and disables button again
        _viewModel.AddMonitoredFolderCommand.Execute(null);
        _viewModel.NewFolderPathInput.Should().BeEmpty();
        _viewModel.AddMonitoredFolderCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task ScanFolderNowAsync_InvokesWatcherReconciler_WhenAvailable()
    {
        var reconcilerMock = new Mock<IWatcherReconciler>();
        var viewModel = new FoldersViewModel(_ruleRepoMock.Object, null, null, null, reconcilerMock.Object);

        File.WriteAllText(Path.Combine(_tempTestDir, "item1.txt"), "data");

        var item = new MonitoredFolderItemViewModel
        {
            Path = _tempTestDir,
            IncludeSubfolders = true,
            ExclusionPatterns = new ObservableCollection<string> { "*.bak" }
        };

        await viewModel.ScanFolderNowAsync(item);

        reconcilerMock.Verify(r => r.ReconcileFolderAsync(
            _tempTestDir,
            true,
            It.Is<IReadOnlyList<string>>(p => p.Contains("*.bak")),
            default), Times.Once);

        item.IsScanning.Should().BeFalse();
        viewModel.StatusMessage.Should().Contain("Scan complete: found 1 active files");
    }

    [Fact]
    public async Task ScanFolderNowAsync_DoesNotScan_WhenFolderCannotBeScanned()
    {
        var reconcilerMock = new Mock<IWatcherReconciler>();
        var viewModel = new FoldersViewModel(_ruleRepoMock.Object, null, null, null, reconcilerMock.Object);

        var item = new MonitoredFolderItemViewModel
        {
            Path = _tempTestDir,
            Status = FolderStatus.AccessDenied
        };

        await viewModel.ScanFolderNowAsync(item);

        reconcilerMock.Verify(r => r.ReconcileFolderAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);

        item.IsScanning.Should().BeFalse();
        viewModel.StatusMessage.Should().Contain("Cannot scan");
    }
    [Fact]
    public async Task AddMonitoredFolderAsync_PersistsToCatalog_AndSurvivesReload()
    {
        var catalogMock = new Mock<IMonitoredFolderCatalog>();
        catalogMock.Setup(c => c.Folders).Returns(new ReadOnlyObservableCollection<MonitoredFolderState>(new ObservableCollection<MonitoredFolderState>()));
        var fileMonitorMock = new Mock<IFileMonitorService>();
        var viewModel = new FoldersViewModel(
            _ruleRepoMock.Object,
            null,
            catalogMock.Object,
            null,
            null,
            fileMonitorMock.Object);

        viewModel.NewFolderPathInput = _tempTestDir;
        viewModel.NewFolderIncludeSubfolders = true;
        viewModel.NewFolderExclusionsInput = "*.log, test";

        await viewModel.AddMonitoredFolderAsync();

        catalogMock.Verify(c => c.AddStandaloneFolderAsync(
            _tempTestDir,
            true,
            It.Is<IEnumerable<string>>(ex => ex.Contains("*.log") && ex.Contains("test")),
            default), Times.Once);

        catalogMock.Verify(c => c.RefreshAsync(default), Times.Once);
        viewModel.StatusMessage.Should().Contain("Added monitored directory");
        viewModel.StatusMessage.Should().Contain("Create a rule");
    }

    [Fact]
    public async Task TogglePauseWatcherAsync_DispatchesToFileMonitorService_AndPersistsToCatalog()
    {
        var catalogMock = new Mock<IMonitoredFolderCatalog>();
        var fileMonitorMock = new Mock<IFileMonitorService>();
        var viewModel = new FoldersViewModel(
            _ruleRepoMock.Object,
            null,
            catalogMock.Object,
            null,
            null,
            fileMonitorMock.Object);

        var item = new MonitoredFolderItemViewModel
        {
            Path = _tempTestDir,
            Exists = true,
            HasReadPermission = true,
            HasWritePermission = true,
            IsPaused = false,
            WatcherStatus = "Watching"
        };

        // 1. Pause
        await viewModel.TogglePauseWatcherAsync(item);

        item.IsPaused.Should().BeTrue();
        item.WatcherStatus.Should().Be("Paused");
        fileMonitorMock.Verify(m => m.PauseFolder(_tempTestDir), Times.Once);
        catalogMock.Verify(c => c.SetFolderPausedAsync(_tempTestDir, true, default), Times.Once);

        // 2. Resume
        await viewModel.TogglePauseWatcherAsync(item);

        item.IsPaused.Should().BeFalse();
        item.WatcherStatus.Should().Be("Watching");
        fileMonitorMock.Verify(m => m.ResumeFolder(_tempTestDir), Times.Once);
        catalogMock.Verify(c => c.SetFolderPausedAsync(_tempTestDir, false, default), Times.Once);
    }

    [Fact]
    public async Task TogglePauseWatcherAsync_RevertsOptimisticUI_WhenPersistenceFails()
    {
        var catalogMock = new Mock<IMonitoredFolderCatalog>();
        catalogMock.Setup(c => c.SetFolderPausedAsync(It.IsAny<string>(), It.IsAny<bool>(), default))
            .ThrowsAsync(new IOException("Simulated storage failure"));

        var viewModel = new FoldersViewModel(
            _ruleRepoMock.Object,
            null,
            catalogMock.Object,
            null,
            null,
            null);

        var item = new MonitoredFolderItemViewModel
        {
            Path = _tempTestDir,
            Exists = true,
            HasReadPermission = true,
            HasWritePermission = true,
            IsPaused = false,
            WatcherStatus = "Watching"
        };

        await viewModel.TogglePauseWatcherAsync(item);

        // Reverted!
        item.IsPaused.Should().BeFalse();
        item.WatcherStatus.Should().Be("Watching");
        viewModel.StatusMessage.Should().Contain("Failed to change pause state");
    }

    [Fact]
    public void EditLinkedRules_WhenFolderHasNoRules_InvokesCreateRuleCallback_OrShowsGuidance()
    {
        string? callbackPath = null;
        var viewModel = new FoldersViewModel(_ruleRepoMock.Object)
        {
            RequestCreateRuleForFolderCallback = p => callbackPath = p
        };

        var item = new MonitoredFolderItemViewModel
        {
            Path = _tempTestDir,
            LinkedRulesCount = 0
        };

        viewModel.EditLinkedRulesCommand.Execute(item);

        callbackPath.Should().Be(_tempTestDir);
    }
}
