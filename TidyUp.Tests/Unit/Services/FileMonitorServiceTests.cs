using FluentAssertions;
using TidyUp.Models.Domain;
using TidyUp.Services;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class FileMonitorServiceTests : IDisposable
{
    private readonly string _testTempDir;

    public FileMonitorServiceTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "TidyUp_FMS_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
            // Ignore test cleanup exceptions
        }
    }

    [Fact]
    public async Task UpdateRulesAsync_InstantiatesFreshNonCancelledToken_AndResumesMonitoring()
    {
        // Arrange
        using var appCts = new CancellationTokenSource();
        using var monitorService = new FileMonitorService();

        var initialRule = new Rule
        {
            Name = "Initial Rule",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = _testTempDir, IncludeSubfolders = false } }
        };

        // Act 1: Start with initial rule and external lifetime token
        await monitorService.StartAsync(new List<Rule> { initialRule }, appCts.Token);

        monitorService.IsRunning.Should().BeTrue();
        monitorService.IsRunTokenCancelled.Should().BeFalse("Run token must be active upon initial start");

        // Act 2: Update rules (which stops and restarts monitoring internally)
        var updatedRule = new Rule
        {
            Name = "Updated Rule",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = _testTempDir, IncludeSubfolders = true } }
        };

        await monitorService.UpdateRulesAsync(new List<Rule> { updatedRule });

        // Assert: After update, monitor is running and run token is fresh, NOT cancelled (Fixes TU-34)
        monitorService.IsRunning.Should().BeTrue("Monitoring should remain running after UpdateRulesAsync");
        monitorService.IsRunTokenCancelled.Should().BeFalse("Run token must be fresh and not bound to an already-cancelled token");

        // Act 3: Explicit stop
        await monitorService.StopAsync();
        monitorService.IsRunning.Should().BeFalse("Monitoring should be stopped");
        monitorService.IsRunTokenCancelled.Should().BeTrue("Run token must be cancelled after stop");
    }

    [Fact]
    public async Task ApplicationStoppingToken_WhenCancelled_PropagatesToRunLoop()
    {
        // Arrange
        using var appCts = new CancellationTokenSource();
        using var monitorService = new FileMonitorService();

        var rule = new Rule
        {
            Name = "App Stopping Test",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = _testTempDir } }
        };

        await monitorService.StartAsync(new List<Rule> { rule }, appCts.Token);
        monitorService.IsRunTokenCancelled.Should().BeFalse();

        // Act: Cancel the external application lifetime token
        appCts.Cancel();

        // Assert: Linked internal run token must observe the cancellation
        monitorService.IsRunTokenCancelled.Should().BeTrue("Linked token must cancel when the application lifetime token cancels");

        await monitorService.StopAsync();
    }

    [Fact]
    public async Task Repeated_Start_Update_Stop_Cycles_SucceedWithoutErrors()
    {
        // Arrange
        using var monitorService = new FileMonitorService();
        var rule = new Rule
        {
            Name = "Cycle Rule",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = _testTempDir } }
        };

        // Repeat cycle multiple times
        for (int i = 0; i < 3; i++)
        {
            await monitorService.StartAsync(new List<Rule> { rule });
            monitorService.IsRunning.Should().BeTrue();
            monitorService.IsRunTokenCancelled.Should().BeFalse();

            await monitorService.UpdateRulesAsync(new List<Rule> { rule });
            monitorService.IsRunning.Should().BeTrue();
            monitorService.IsRunTokenCancelled.Should().BeFalse();

            await monitorService.StopAsync();
            monitorService.IsRunning.Should().BeFalse();
        }
    }

    #region TASK-REM-26 (TU-35): Multilateral Watcher Scope Merging Tests

    [Fact]
    public async Task StartAsync_MergesWatcherScope_WhenMultipleRulesMonitorSameDirectoryWithDifferentRecursion()
    {
        // Arrange: Rule A is top-level only; Rule B monitors recursively
        using var monitorService = new FileMonitorService();

        var ruleA = new Rule
        {
            Name = "Rule A (Top-level only)",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = _testTempDir, IncludeSubfolders = false } }
        };

        var ruleB = new Rule
        {
            Name = "Rule B (Recursive)",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = _testTempDir, IncludeSubfolders = true } }
        };

        // Act
        await monitorService.StartAsync(new List<Rule> { ruleA, ruleB });

        // Assert: Merged watcher must have IncludeSubdirectories = true so Rule B receives subfolder events (Fixes TU-35)
        monitorService.TryGetWatcherIncludeSubdirectories(_testTempDir, out var includeSubdirs).Should().BeTrue();
        includeSubdirs.Should().BeTrue("Shared watcher must be configured as a superset with IncludeSubdirectories=true when ANY rule requires recursion");

        await monitorService.StopAsync();
    }

    [Fact]
    public async Task ScanAllFoldersAsync_DispatchesSubfolderFiles_WhenOneRuleIsRecursiveAndOtherIsTopLevel()
    {
        // Arrange:
        // Rule A is top-level only and excludes .log files
        // Rule B is recursive and does not exclude .log files
        var subDir = Path.Combine(_testTempDir, "subfolder");
        Directory.CreateDirectory(subDir);

        var topTxt = Path.Combine(_testTempDir, "top.txt");
        var topLog = Path.Combine(_testTempDir, "top.log");
        var subTxt = Path.Combine(subDir, "nested.txt");
        var subLog = Path.Combine(subDir, "nested.log");

        File.WriteAllText(topTxt, "top txt");
        File.WriteAllText(topLog, "top log");
        File.WriteAllText(subTxt, "sub txt");
        File.WriteAllText(subLog, "sub log");

        using var monitorService = new FileMonitorService();

        var ruleA = new Rule
        {
            Name = "Rule A",
            IsEnabled = true,
            MonitoredFolders =
            {
                new MonitoredFolder
                {
                    Path = _testTempDir,
                    IncludeSubfolders = false,
                    ExclusionPatterns = { ".log" }
                }
            }
        };

        var ruleB = new Rule
        {
            Name = "Rule B",
            IsEnabled = true,
            MonitoredFolders =
            {
                new MonitoredFolder
                {
                    Path = _testTempDir,
                    IncludeSubfolders = true
                }
            }
        };

        var detectedFiles = new List<string>();
        monitorService.FileDetected += (s, e) => detectedFiles.Add(e.FileInfo.FullName);

        // Act
        await monitorService.StartAsync(new List<Rule> { ruleA, ruleB });

        // Assert:
        // top.txt is eligible for Rule A and Rule B -> detected
        // top.log is excluded by Rule A but eligible for Rule B -> detected (Rule A exclusion does not clobber Rule B)
        // sub/nested.txt is eligible for Rule B -> detected (Rule A top-level setting does not clobber Rule B subfolder watching)
        // sub/nested.log is eligible for Rule B -> detected
        detectedFiles.Should().Contain(topTxt);
        detectedFiles.Should().Contain(topLog);
        detectedFiles.Should().Contain(subTxt);
        detectedFiles.Should().Contain(subLog);

        await monitorService.StopAsync();
    }

    [Fact]
    public void IsFileEligibleForFolder_RespectsCanonicalSeparatorsAndExclusions()
    {
        // Arrange
        using var monitorService = new FileMonitorService();

        var topFolder = new MonitoredFolder
        {
            Path = _testTempDir,
            IncludeSubfolders = false,
            ExclusionPatterns = { "*.bak", "ignore" }
        };

        var recursiveFolder = new MonitoredFolder
        {
            Path = _testTempDir,
            IncludeSubfolders = true,
            ExclusionPatterns = { "*.bak" }
        };

        var topFile = Path.Combine(_testTempDir, "document.pdf");
        var subFile = Path.Combine(_testTempDir, "nested", "document.pdf");
        var excludedFile = Path.Combine(_testTempDir, "backup.bak");

        // Assert
        monitorService.IsFileEligibleForFolder(topFile, topFolder).Should().BeTrue();
        monitorService.IsFileEligibleForFolder(subFile, topFolder).Should().BeFalse("Top-level folder must not match subfolder file");

        monitorService.IsFileEligibleForFolder(topFile, recursiveFolder).Should().BeTrue();
        monitorService.IsFileEligibleForFolder(subFile, recursiveFolder).Should().BeTrue("Recursive folder must match subfolder file");

        monitorService.IsFileEligibleForFolder(excludedFile, topFolder).Should().BeFalse();
        monitorService.IsFileEligibleForFolder(excludedFile, recursiveFolder).Should().BeFalse();
    }

    #endregion
}

