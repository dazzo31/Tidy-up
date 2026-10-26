using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TidyUp.Data.Entities;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Diagnostics;
using TidyUp.Services.Processing;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class OperationTelemetryConvergenceTests
{
    [Fact]
    public async Task OperationTelemetryService_RecordsDurableLogAndDeduplicatesDuplicateEvents()
    {
        var logRepoMock = new Mock<IActionLogRepository>();
        var telemetryService = new OperationTelemetryService(logRepoMock.Object);

        var actionId = Guid.NewGuid();
        var telemetryEvent = new OperationTelemetryEvent
        {
            ActionId = actionId,
            RuleName = "Test Rule",
            ActionType = "Move",
            SourcePath = @"C:\Test\file.txt",
            TargetPath = @"C:\Dest\file.txt",
            Outcome = "Success",
            Timestamp = DateTime.UtcNow
        };

        var eventFiredCount = 0;
        telemetryService.OperationExecuted += (s, e) => eventFiredCount++;

        // First execution -> should save and fire event
        await telemetryService.RecordOperationAsync(telemetryEvent);

        logRepoMock.Verify(r => r.AddAsync(It.Is<ActionLogEntity>(e => e.Id == actionId && e.RuleName == "Test Rule")), Times.Once);
        eventFiredCount.Should().Be(1);

        // Duplicate delivery with same actionId -> should be ignored (deduplicated)
        await telemetryService.RecordOperationAsync(telemetryEvent);

        logRepoMock.Verify(r => r.AddAsync(It.IsAny<ActionLogEntity>()), Times.Once);
        eventFiredCount.Should().Be(1);
    }

    [Fact]
    public async Task DashboardViewModel_SubscribedToTelemetry_UpdatesMetricsAndRecentActivityLive()
    {
        var ruleRepoMock = new Mock<IRuleRepository>();
        var logRepoMock = new Mock<IActionLogRepository>();
        var monitorServiceMock = new Mock<IFileMonitorService>();
        var telemetryService = new OperationTelemetryService(logRepoMock.Object);

        var vm = new DashboardViewModel(
            ruleRepoMock.Object,
            logRepoMock.Object,
            monitorServiceMock.Object,
            telemetryService: telemetryService);

        vm.TotalFilesProcessed.Should().Be(0);
        vm.TotalSuccess.Should().Be(0);
        vm.RecentActivity.Should().BeEmpty();

        var evt1 = new OperationTelemetryEvent
        {
            ActionId = Guid.NewGuid(),
            RuleName = "Organize PDFs",
            ActionType = "Move",
            SourcePath = @"C:\Downloads\doc1.pdf",
            TargetPath = @"C:\Documents\doc1.pdf",
            Outcome = "Success",
            Timestamp = DateTime.UtcNow
        };

        await telemetryService.RecordOperationAsync(evt1);

        vm.TotalFilesProcessed.Should().Be(1);
        vm.TotalSuccess.Should().Be(1);
        vm.TotalErrors.Should().Be(0);
        vm.SuccessRateFormatted.Should().Be("100.0%");
        vm.RecentActivity.Should().HaveCount(1);
        vm.RecentActivity[0].FileName.Should().Be("doc1.pdf");
        vm.RecentActivity[0].RuleName.Should().Be("Organize PDFs");

        var evt2 = new OperationTelemetryEvent
        {
            ActionId = Guid.NewGuid(),
            RuleName = "Organize Images",
            ActionType = "Copy",
            SourcePath = @"C:\Downloads\pic.png",
            Outcome = "Error",
            ErrorMessage = "Disk full",
            Timestamp = DateTime.UtcNow
        };

        await telemetryService.RecordOperationAsync(evt2);

        vm.TotalFilesProcessed.Should().Be(2);
        vm.TotalSuccess.Should().Be(1);
        vm.TotalErrors.Should().Be(1);
        vm.SuccessRateFormatted.Should().Be("50.0%");
        vm.RecentActivity.Should().HaveCount(2);
        vm.RecentActivity[0].FileName.Should().Be("pic.png");
        vm.RecentActivity[0].Status.Should().Be("Error");
    }

    [Fact]
    public async Task LogsViewModel_SubscribedToTelemetry_AppendsMatchingLiveEvents()
    {
        var logRepoMock = new Mock<IActionLogRepository>();
        var telemetryService = new OperationTelemetryService(logRepoMock.Object);

        var vm = new LogsViewModel(logRepoMock.Object, telemetryService);
        vm.SelectedStatus = "All";
        vm.SelectedRule = "All Rules";

        vm.TotalCount.Should().Be(0);
        vm.Logs.Should().BeEmpty();

        var evt = new OperationTelemetryEvent
        {
            ActionId = Guid.NewGuid(),
            RuleName = "Clean Old Files",
            ActionType = "Delete",
            SourcePath = @"C:\Temp\old.log",
            Outcome = "Success",
            Timestamp = DateTime.UtcNow
        };

        await telemetryService.RecordOperationAsync(evt);

        vm.TotalCount.Should().Be(1);
        vm.SuccessCount.Should().Be(1);
        vm.Logs.Should().HaveCount(1);
        vm.Logs[0].FilePath.Should().Be(@"C:\Temp\old.log");
        vm.AvailableRules.Should().Contain("Clean Old Files");
    }

    [Fact]
    public async Task BatchProcessingCoordinator_UpdatesRuleStatisticsWithDistinctInputFileCount()
    {
        var actionExecutorMock = new Mock<IActionExecutor>();
        var ruleRepoMock = new Mock<IRuleRepository>();

        var coordinator = new BatchProcessingCoordinator(
            actionExecutorMock.Object,
            ruleRepository: ruleRepoMock.Object);

        var ruleId = Guid.NewGuid();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Two chained actions on the SAME input file (Rename -> Move)
            var action1 = new PlannedFileAction
            {
                Id = Guid.NewGuid(),
                RuleId = ruleId,
                SourcePath = tempFile,
                InitialSourcePath = tempFile,
                FileAction = new RenameFileAction()
            };
            var action2 = new PlannedFileAction
            {
                Id = Guid.NewGuid(),
                RuleId = ruleId,
                SourcePath = tempFile,
                InitialSourcePath = tempFile,
                FileAction = new MoveFileAction()
            };

            actionExecutorMock.Setup(e => e.ExecutePlannedActionAsync(It.IsAny<PlannedFileAction>(), It.IsAny<Guid>()))
                .ReturnsAsync(new ActionResult { Success = true });

            var result = await coordinator.ExecuteBatchAsync(new[] { action1, action2 }, Guid.NewGuid());

            result.Status.Should().Be(BatchExecutionStatus.Completed);
            result.SucceededCount.Should().Be(2);

            // Distinct input files processed must be 1, NOT 2 (Rename + Move on same file counts as 1 processed file)
            ruleRepoMock.Verify(r => r.RecordRuleExecutionAsync(
                ruleId,
                1,
                It.Is<DateTime>(d => (DateTime.UtcNow - d).TotalSeconds < 5)),
                Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
