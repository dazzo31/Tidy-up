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
}

