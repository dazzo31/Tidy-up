using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data;
using TidyUp.Models.Domain;
using TidyUp.Services;
using TidyUp.Services.Simulation;
using TidyUp.Tests.TestInfrastructure;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

/// <summary>
/// The rule list should say what a rule covers, and failure states should look like failure states.
/// </summary>
public class RuleListAndEmptyStateTests
{
    [Fact]
    public void RuleListSubtitle_LeadsWithTheFolderCount_ThenTheConditionSummary()
    {
        var rule = new Rule { Summary = "Move .pdf files to Documents" };
        rule.MonitoredFolders.Add(new MonitoredFolder { Path = @"C:\a" });
        rule.MonitoredFolders.Add(new MonitoredFolder { Path = @"C:\b" });

        rule.ListSubtitle.Should().Be("2 folders \u00b7 Move .pdf files to Documents");
    }

    [Fact]
    public void RuleListSubtitle_SaysOneFolderInTheSingular()
    {
        var rule = new Rule { Summary = "Anything" };
        rule.MonitoredFolders.Add(new MonitoredFolder { Path = @"C:\a" });

        rule.ListSubtitle.Should().StartWith("1 folder \u00b7");
    }

    [Fact]
    public void RuleListSubtitle_IsJustTheFolderCount_WhenThereIsNoSummaryYet()
    {
        var rule = new Rule();

        rule.ListSubtitle.Should().Be("0 folders");
    }

    [Fact]
    public void RulePriorityTooltip_ExplainsTheOrder_AndReportsTheLastRunFileCount()
    {
        var neverRun = new Rule { ExecutionOrder = 3 };
        neverRun.ExecutionOrderTooltip.Should().Contain("priority 3").And.Contain("No files matched");

        var ran = new Rule { ExecutionOrder = 1, LastRunDate = new DateTime(2026, 10, 17, 9, 30, 0, DateTimeKind.Utc), FilesProcessedCount = 42 };
        ran.ExecutionOrderTooltip.Should().Contain("priority 1").And.Contain("42 file(s)");
    }

    [Fact]
    public void ApplyButtonText_NeverReadsLikeAnEmptyCounter()
    {
        var vm = CreatePreviewViewModel();

        vm.ApprovedCount = 0;
        vm.ApplyButtonText.Should().Be("Apply approved changes (none selected)");

        vm.ApprovedCount = 1;
        vm.ApplyButtonText.Should().Be("Apply 1 approved change");

        vm.ApprovedCount = 7;
        vm.ApplyButtonText.Should().Be("Apply 7 approved changes");
    }

    [Fact]
    public void ApplyButtonText_RaisesChangeNotificationWhenTheCountChanges()
    {
        var vm = CreatePreviewViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ApprovedCount = 3;

        changed.Should().Contain(nameof(PreviewViewModel.ApplyButtonText));
    }

    [Fact]
    public async Task HistoryViewModel_SurfacesLoadFailures_InsteadOfAnEmptyListOrAnException()
    {
        // A context whose journal table does not exist stands in for a corrupt/unmigrated database.
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var context = new TidyUpDbContext(options);
        await using var _ = context;

        var vm = new HistoryViewModel(new Moq.Mock<TidyUp.Services.Rollback.IRollbackEngine>().Object, directContext: context);
        vm.HasLoadError.Should().BeFalse();

        await vm.LoadPageAsync();

        vm.HasLoadError.Should().BeTrue();
        vm.LoadErrorMessage.Should().NotBeNullOrWhiteSpace();
        vm.LoadErrorDetails.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task History_CopyErrorDetailsCommand_ReportsInsteadOfThrowing_WhenTheClipboardIsUnavailable()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var context = new TidyUpDbContext(options);
        await using var _ = context;

        var vm = new HistoryViewModel(new Moq.Mock<TidyUp.Services.Rollback.IRollbackEngine>().Object, directContext: context)
        {
            LoadErrorMessage = "boom"
        };

        // Headless (no STA message pump): the clipboard is unavailable, so the command must report, not throw.
        var act = () => vm.CopyErrorDetails();
        act.Should().NotThrow();
        vm.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    private static PreviewViewModel CreatePreviewViewModel() =>
        new(new Moq.Mock<IExecutionPlanGenerator>().Object, new Moq.Mock<IActionExecutor>().Object);

    [Fact]
    public void EmptyStateControl_ExposesAnErrorVariantWithDetails()
    {
        var xaml = File.ReadAllText(AppSourceFiles.Find("Controls/EmptyStateControl.xaml"));
        xaml.Should().Contain("DetailsPanel").And.Contain("DetailsText").And.Contain("SecondaryButton");

        var code = File.ReadAllText(AppSourceFiles.Find("Controls/EmptyStateControl.xaml.cs"));
        code.Should().Contain("EmptyStateVariant").And.Contain("Error");
    }
}
