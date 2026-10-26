using TidyUp.Configuration;
using FluentAssertions;
using Moq;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Folders;
using TidyUp.Services.Folders;
using TidyUp.Services.Monitoring;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class MonitoredFolderCatalogTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly Mock<IRuleRepository> _ruleRepoMock = new();
    private readonly Mock<IWatcherHealthMonitor> _healthMonitorMock = new();
    private readonly FolderPathNormalizer _normalizer = new();

    public MonitoredFolderCatalogTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "TidyUp_CatalogTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);

        _healthMonitorMock.Setup(m => m.GetAllFolderHealth())
            .Returns(new Dictionary<string, FolderHealthReport>());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task RefreshAsync_CombinesMultipleRulesWithDifferentCasingOrSlashes_SingleCanonicalEntry()
    {
        // Arrange
        var rule1Id = Guid.NewGuid();
        var rule2Id = Guid.NewGuid();

        var path1 = _tempFolder.ToLowerInvariant() + Path.DirectorySeparatorChar;
        var path2 = _tempFolder.ToUpperInvariant();

        var rule1 = new Rule
        {
            Id = rule1Id,
            Name = "Rule One",
            IsEnabled = true,
            MonitoredFolders =
            {
                new MonitoredFolder { Path = path1, IncludeSubfolders = false, ExclusionPatterns = { "*.tmp" } }
            }
        };

        var rule2 = new Rule
        {
            Id = rule2Id,
            Name = "Rule Two",
            IsEnabled = false,
            MonitoredFolders =
            {
                new MonitoredFolder { Path = path2, IncludeSubfolders = true, ExclusionPatterns = { "*.bak", "*.tmp" } }
            }
        };

        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule> { rule1, rule2 });

        var validator = new FolderValidator();
        using var catalog = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object);

        // Act
        await catalog.RefreshAsync();

        // Assert
        catalog.Folders.Should().HaveCount(1);
        var folder = catalog.Folders.First();

        folder.TotalRuleCount.Should().Be(2);
        folder.EnabledRuleCount.Should().Be(1);
        folder.IncludeSubfolders.Should().BeTrue();
        folder.LinkedRuleNames.Should().Contain("Rule One").And.Contain("Rule Two");
        folder.ExclusionPatterns.Should().BeEquivalentTo(new[] { "*.bak", "*.tmp" });
        folder.ValidationStatus.Should().Be(FolderValidationStatus.Accessible);
        folder.WatcherHealth.Should().Be(WatcherHealthStatus.Healthy);
        folder.HasAlert.Should().BeFalse();

        catalog.Summary.TotalFolders.Should().Be(1);
        catalog.Summary.DistinctAlertCount.Should().Be(0);

        _healthMonitorMock.Verify(m => m.RegisterFolder(folder.NormalizedPath, false), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_MissingDirectory_MarksAsMissingAndDistinctAlert()
    {
        // Arrange
        var missingPath = Path.Combine(Path.GetTempPath(), "NonExistent_" + Guid.NewGuid().ToString("N"));
        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Missing Rule",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = missingPath } }
        };

        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule> { rule });

        var validator = new FolderValidator();
        using var catalog = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object);

        // Act
        await catalog.RefreshAsync();

        // Assert
        catalog.Folders.Should().HaveCount(1);
        var folder = catalog.Folders.First();

        folder.Exists.Should().BeFalse();
        folder.ValidationStatus.Should().Be(FolderValidationStatus.Missing);
        folder.HasAlert.Should().BeTrue();
        folder.HealthSummary.Should().Contain("does not exist");

        catalog.Summary.TotalFolders.Should().Be(1);
        catalog.Summary.MissingFolders.Should().Be(1);
        catalog.Summary.DistinctAlertCount.Should().Be(1);

        // Crucial invariant: missing directory must NOT be registered as active in watcher
        _healthMonitorMock.Verify(m => m.RegisterFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_AllRulesDisabled_RegistersAsPaused()
    {
        // Arrange
        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Disabled Rule",
            IsEnabled = false,
            MonitoredFolders = { new MonitoredFolder { Path = _tempFolder } }
        };

        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule> { rule });

        var validator = new FolderValidator();
        using var catalog = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object);

        // Act
        await catalog.RefreshAsync();

        // Assert
        catalog.Folders.Should().HaveCount(1);
        var folder = catalog.Folders.First();

        folder.EnabledRuleCount.Should().Be(0);
        folder.WatcherHealth.Should().Be(WatcherHealthStatus.Paused);
        folder.HealthSummary.Should().Be("Monitoring paused");

        _healthMonitorMock.Verify(m => m.RegisterFolder(folder.NormalizedPath, true), Times.Once);
    }

    [Fact]
    public async Task RulesChanged_Event_AutomaticallyRefreshesCatalogAndNotifiesSubscribers()
    {
        // Arrange
        var rules = new List<Rule>();
        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(() => rules);

        var validator = new FolderValidator();
        using var catalog = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object);

        await catalog.RefreshAsync();
        catalog.Folders.Should().BeEmpty();

        var tcs = new TaskCompletionSource<FolderCatalogChangedEventArgs>();
        catalog.CatalogChanged += (s, e) => tcs.TrySetResult(e);

        // Act: Add a rule and raise RulesChanged
        var addedRule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Dynamic Rule",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = _tempFolder + @"\" } }
        };
        rules.Add(addedRule);

        _ruleRepoMock.Raise(
            r => r.RulesChanged += null,
            _ruleRepoMock.Object,
            new RulesChangedEventArgs { AffectedRuleId = addedRule.Id, ChangeType = "Added" });

        var eventArgs = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        eventArgs.Folders.Should().ContainSingle();
        catalog.Folders.Should().ContainSingle();
        catalog.Folders[0].NormalizedPath.Should().Be(_tempFolder);
        catalog.Folders[0].LinkedRuleNames.Should().Contain("Dynamic Rule");
    }

    [Fact]
    public async Task DashboardAndFoldersViewModels_WithSharedCatalog_ProduceIdenticalPathsAndCounts()
    {
        // Arrange
        var rule1 = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Rule A",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = _tempFolder.ToLowerInvariant() + @"\", IncludeSubfolders = false, ExclusionPatterns = { "*.tmp" } } }
        };
        var rule2 = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Rule B",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = _tempFolder.ToUpperInvariant(), IncludeSubfolders = true, ExclusionPatterns = { "*.log" } } }
        };

        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule> { rule1, rule2 });

        var logRepoMock = new Mock<IActionLogRepository>();
        logRepoMock.Setup(l => l.GetStatisticsAsync()).ReturnsAsync(new TidyUp.Data.Repositories.LogStatistics());
        logRepoMock.Setup(l => l.GetFilteredAsync(null, null, null, null, null, 20))
            .ReturnsAsync(new List<TidyUp.Data.Entities.ActionLogEntity>());

        var monitorServiceMock = new Mock<TidyUp.Services.IFileMonitorService>();

        var validator = new FolderValidator();
        using var catalog = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object);

        var dashboardVm = new TidyUp.ViewModels.DashboardViewModel(
            _ruleRepoMock.Object,
            logRepoMock.Object,
            monitorServiceMock.Object,
            null,
            _healthMonitorMock.Object,
            catalog,
            _normalizer);

        var foldersVm = new TidyUp.ViewModels.FoldersViewModel(
            _ruleRepoMock.Object,
            _healthMonitorMock.Object,
            catalog,
            _normalizer);

        // Act
        await dashboardVm.LoadDashboardDataAsync();
        await foldersVm.LoadFoldersAsync();

        // Assert
        dashboardVm.MonitoredFolders.Should().ContainSingle();
        foldersVm.MonitoredFolders.Should().ContainSingle();

        dashboardVm.MonitoredFolders[0].Path.Should().Be(foldersVm.MonitoredFolders[0].Path);
        dashboardVm.MonitoredFolders[0].Path.Should().BeEquivalentTo(_tempFolder);
        dashboardVm.MonitoredFolders[0].RuleCount.Should().Be(foldersVm.MonitoredFolders[0].LinkedRulesCount).And.Be(2);
        dashboardVm.MonitoredFolders[0].IncludeSubfolders.Should().Be(foldersVm.MonitoredFolders[0].IncludeSubfolders).And.BeTrue();
        dashboardVm.MonitoredFolders[0].ExclusionPatterns.Should().BeEquivalentTo(foldersVm.MonitoredFolders[0].ExclusionPatterns);
    }
    [Fact]
    public async Task AddStandaloneFolderAsync_PersistsToDisk_AndSurvivesCatalogReconstruction()
    {
        var paths = new TidyUpPaths(_tempFolder);
        var validator = new FolderValidator();
        var standaloneFolder = Path.Combine(_tempFolder, "StandaloneDir");
        Directory.CreateDirectory(standaloneFolder);

        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule>());

        Guid stableId;
        using (var catalog1 = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object,
            paths))
        {
            var added = await catalog1.AddStandaloneFolderAsync(
                standaloneFolder,
                includeSubfolders: true,
                new[] { "*.bak", "cache" });

            added.Should().NotBeNull();
            added.IsStandalone.Should().BeTrue();
            added.TotalRuleCount.Should().Be(0);
            added.IncludeSubfolders.Should().BeTrue();
            added.ExclusionPatterns.Should().BeEquivalentTo(new[] { "*.bak", "cache" });
            stableId = added.Id;
            stableId.Should().NotBeEmpty();
        }

        // Recreate catalog from same disk paths (simulating app restart)
        using (var catalog2 = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object,
            paths))
        {
            await catalog2.RefreshAsync();

            catalog2.Folders.Should().ContainSingle();
            var reloaded = catalog2.Folders[0];
            reloaded.Id.Should().Be(stableId);
            reloaded.IsStandalone.Should().BeTrue();
            reloaded.TotalRuleCount.Should().Be(0);
            reloaded.IncludeSubfolders.Should().BeTrue();
            reloaded.ExclusionPatterns.Should().BeEquivalentTo(new[] { "*.bak", "cache" });
            reloaded.WatcherHealth.Should().Be(WatcherHealthStatus.Healthy);
        }
    }

    [Fact]
    public async Task SetFolderPausedAsync_PersistsPausedState_AcrossCatalogReconstruction()
    {
        var paths = new TidyUpPaths(_tempFolder);
        var validator = new FolderValidator();
        var testFolder = Path.Combine(_tempFolder, "WatchDir");
        Directory.CreateDirectory(testFolder);

        var rule = new Rule
        {
            Name = "Active Rule",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = testFolder, IncludeSubfolders = true } }
        };
        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule> { rule });

        using (var catalog1 = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object,
            paths))
        {
            await catalog1.RefreshAsync();
            catalog1.Folders.Should().ContainSingle();
            catalog1.Folders[0].WatcherHealth.Should().Be(WatcherHealthStatus.Healthy);

            // Pause folder explicitly
            await catalog1.SetFolderPausedAsync(testFolder, true);
            catalog1.Folders[0].WatcherHealth.Should().Be(WatcherHealthStatus.Paused);
            catalog1.IsFolderExplicitlyPaused(testFolder).Should().BeTrue();
        }

        // Reconstruct catalog (simulating app restart)
        using (var catalog2 = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object,
            paths))
        {
            await catalog2.RefreshAsync();

            catalog2.Folders.Should().ContainSingle();
            var reloaded = catalog2.Folders[0];
            reloaded.WatcherHealth.Should().Be(WatcherHealthStatus.Paused);
            catalog2.IsFolderExplicitlyPaused(testFolder).Should().BeTrue();

            // Resume folder
            await catalog2.SetFolderPausedAsync(testFolder, false);
            catalog2.Folders[0].WatcherHealth.Should().Be(WatcherHealthStatus.Healthy);
            catalog2.IsFolderExplicitlyPaused(testFolder).Should().BeFalse();
        }
    }

    [Fact]
    public async Task RemoveStandaloneFolderAsync_RemovesFromDiskAndCatalog()
    {
        var paths = new TidyUpPaths(_tempFolder);
        var validator = new FolderValidator();
        var standaloneFolder = Path.Combine(_tempFolder, "TempStandalone");
        Directory.CreateDirectory(standaloneFolder);

        _ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule>());

        using var catalog = new MonitoredFolderCatalog(
            _ruleRepoMock.Object,
            _normalizer,
            validator,
            _healthMonitorMock.Object,
            paths);

        await catalog.AddStandaloneFolderAsync(standaloneFolder);
        catalog.Folders.Should().ContainSingle();

        var removed = await catalog.RemoveStandaloneFolderAsync(standaloneFolder);
        removed.Should().BeTrue();
        catalog.Folders.Should().BeEmpty();
    }
}
