using FluentAssertions;
using Moq;
using TidyUp.Data.Entities;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Watcher;
using TidyUp.ViewModels;

namespace TidyUp.Tests.Unit.ViewModels;

public class DashboardViewModelTests
{
    private readonly Mock<IRuleRepository> _ruleRepoMock = new();
    private readonly Mock<IActionLogRepository> _logRepoMock = new();
    private readonly Mock<IFileMonitorService> _monitorServiceMock = new();
    private readonly Mock<IWatcherReconciler> _reconcilerMock = new();

    private DashboardViewModel CreateViewModel()
    {
        return new DashboardViewModel(
            _ruleRepoMock.Object,
            _logRepoMock.Object,
            _monitorServiceMock.Object,
            _reconcilerMock.Object);
    }

    [Fact]
    public async Task LoadDashboardDataAsync_AggregatesStatisticsAndRuleMetrics()
    {
        // Arrange
        var rules = new List<Rule>
        {
            new Rule { Name = "Rule 1", IsEnabled = true },
            new Rule { Name = "Rule 2", IsEnabled = true },
            new Rule { Name = "Rule 3", IsEnabled = false }
        };
        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(rules);

        var stats = new LogStatistics
        {
            Total = 150,
            Success = 147,
            Warning = 2,
            Error = 1
        };
        _logRepoMock.Setup(l => l.GetStatisticsAsync()).ReturnsAsync(stats);

        var recentLogs = new List<ActionLogEntity>
        {
            new ActionLogEntity
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                RuleName = "Rule 1",
                FilePath = @"C:\Downloads\file.pdf",
                ActionPerformed = "Moved",
                Status = "Success"
            }
        };
        _logRepoMock.Setup(l => l.GetFilteredAsync(null, null, null, null, null, 20)).ReturnsAsync(recentLogs);

        var vm = CreateViewModel();

        // Act
        await vm.LoadDashboardDataAsync();

        // Assert
        vm.TotalRuleCount.Should().Be(3);
        vm.ActiveRuleCount.Should().Be(2);
        vm.TotalFilesProcessed.Should().Be(150);
        vm.TotalSuccess.Should().Be(147);
        vm.TotalWarnings.Should().Be(2);
        vm.TotalErrors.Should().Be(1);
        vm.SuccessRateFormatted.Should().Be("98.0%");
        vm.RecentActivity.Should().HaveCount(1);
        vm.RecentActivity[0].FileName.Should().Be("file.pdf");
        vm.RecentActivity[0].Status.Should().Be("Success");
    }

    [Fact]
    public async Task LoadDashboardDataAsync_DeduplicatesAndGroupsMonitoredFolders()
    {
        // Arrange: Multiple rules referencing overlapping and unique folders
        var folderA = @"C:\Users\Test\Downloads";
        var folderB = @"C:\Users\Test\Desktop";

        var rule1 = new Rule { Name = "Download PDFs", IsEnabled = true };
        rule1.MonitoredFolders.Add(new MonitoredFolder { Path = folderA, IncludeSubfolders = true });

        var rule2 = new Rule { Name = "Download Images", IsEnabled = true };
        rule2.MonitoredFolders.Add(new MonitoredFolder { Path = folderA, IncludeSubfolders = true });
        rule2.MonitoredFolders.Add(new MonitoredFolder { Path = folderB, IncludeSubfolders = false });

        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule> { rule1, rule2 });
        _logRepoMock.Setup(l => l.GetStatisticsAsync()).ReturnsAsync(new LogStatistics());
        _logRepoMock.Setup(l => l.GetFilteredAsync(null, null, null, null, null, 20)).ReturnsAsync(new List<ActionLogEntity>());

        var vm = CreateViewModel();

        // Act
        await vm.LoadDashboardDataAsync();

        // Assert
        vm.MonitoredFolderCount.Should().Be(2);
        vm.MonitoredFolders.Should().HaveCount(2);

        var downloadsFolder = vm.MonitoredFolders.FirstOrDefault(f => f.Path == folderA);
        downloadsFolder.Should().NotBeNull();
        downloadsFolder!.RuleCount.Should().Be(2);
        downloadsFolder.AssociatedRules.Should().Contain("Download PDFs");
        downloadsFolder.AssociatedRules.Should().Contain("Download Images");

        var desktopFolder = vm.MonitoredFolders.FirstOrDefault(f => f.Path == folderB);
        desktopFolder.Should().NotBeNull();
        desktopFolder!.RuleCount.Should().Be(1);
    }

    [Fact]
    public async Task ScanFolderAsync_InvokesWatcherReconcilerForSpecificPath()
    {
        // Arrange
        var vm = CreateViewModel();
        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule>());
        _logRepoMock.Setup(l => l.GetStatisticsAsync()).ReturnsAsync(new LogStatistics());
        _logRepoMock.Setup(l => l.GetFilteredAsync(null, null, null, null, null, 20)).ReturnsAsync(new List<ActionLogEntity>());

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var folderItem = new MonitoredFolderItem
            {
                Path = tempDir,
                IncludeSubfolders = true,
                ExclusionPatterns = new List<string> { "*.tmp" }
            };

            // Act
            await vm.ScanFolderAsync(folderItem);

            // Assert
            _reconcilerMock.Verify(r => r.ReconcileFolderAsync(
                tempDir,
                true,
                It.Is<IReadOnlyList<string>>(p => p.Contains("*.tmp")),
                default), Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir);
            }
        }
    }

    [Fact]
    public async Task ScanAllFoldersAsync_InvokesFileMonitorService()
    {
        // Arrange
        var vm = CreateViewModel();
        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule>());
        _logRepoMock.Setup(l => l.GetStatisticsAsync()).ReturnsAsync(new LogStatistics());
        _logRepoMock.Setup(l => l.GetFilteredAsync(null, null, null, null, null, 20)).ReturnsAsync(new List<ActionLogEntity>());

        // Act
        await vm.ScanAllFoldersAsync();

        // Assert
        _monitorServiceMock.Verify(m => m.ScanAllFoldersAsync(), Times.Once);
    }

    [Theory]
    [InlineData("RuleEditor")]
    [InlineData("Logs")]
    [InlineData("Settings")]
    public void NavigationCommands_RaiseRequestNavigateWithTargetView(string target)
    {
        // Arrange
        var vm = CreateViewModel();
        NavigationView? requestedView = null;
        vm.RequestNavigate += v => requestedView = v;

        // Act
        switch (target)
        {
            case "RuleEditor":
                vm.NavigateToRuleEditorCommand.Execute(null);
                requestedView.Should().Be(NavigationView.RuleEditor);
                break;
            case "Logs":
                vm.NavigateToLogsCommand.Execute(null);
                requestedView.Should().Be(NavigationView.Logs);
                break;
            case "Settings":
                vm.NavigateToSettingsCommand.Execute(null);
                requestedView.Should().Be(NavigationView.Settings);
                break;
        }
    }
}

