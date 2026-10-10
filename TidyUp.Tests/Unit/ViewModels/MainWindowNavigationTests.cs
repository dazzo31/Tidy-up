using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class MainWindowNavigationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowNavigationTests()
    {
        _serviceProvider = ServiceConfiguration.ConfigureServices();
        _viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
    }

    [Fact]
    public void MainWindowViewModel_InitialView_IsDashboard()
    {
        _viewModel.CurrentView.Should().Be(NavigationView.Dashboard);
        _viewModel.IsDashboardActive.Should().BeTrue();
        _viewModel.IsRulesActive.Should().BeFalse();
        _viewModel.IsFoldersActive.Should().BeFalse();
        _viewModel.IsPreviewActive.Should().BeFalse();
        _viewModel.IsHistoryActive.Should().BeFalse();
        _viewModel.IsSettingsActive.Should().BeFalse();
    }

    [Fact]
    public void NavigateToFoldersCommand_SwitchesViewAndUpdatesActiveFlags()
    {
        _viewModel.NavigateToFoldersCommand.Execute(null);

        _viewModel.CurrentView.Should().Be(NavigationView.Folders);
        _viewModel.IsFoldersActive.Should().BeTrue();
        _viewModel.IsDashboardActive.Should().BeFalse();
        _viewModel.StatusMessage.Should().Be("Monitored Folders Hub");
    }

    [Fact]
    public void NavigateToPreviewReviewCommand_SwitchesViewAndUpdatesActiveFlags()
    {
        _viewModel.NavigateToPreviewReviewCommand.Execute(null);

        _viewModel.CurrentView.Should().Be(NavigationView.PreviewReview);
        _viewModel.IsPreviewActive.Should().BeTrue();
        _viewModel.IsDashboardActive.Should().BeFalse();
        _viewModel.StatusMessage.Should().Be("Preview & Review Workspace");
    }

    [Fact]
    public void NavigateToRuleEditorCommand_SwitchesViewAndUpdatesActiveFlags()
    {
        _viewModel.NavigateToRuleEditorCommand.Execute(null);

        _viewModel.CurrentView.Should().Be(NavigationView.RuleEditor);
        _viewModel.IsRulesActive.Should().BeTrue();
        _viewModel.IsDashboardActive.Should().BeFalse();
    }

    [Fact]
    public void ViewHistoryCommand_SwitchesViewAndUpdatesActiveFlags()
    {
        _viewModel.ViewHistoryCommand.Execute(null);

        _viewModel.CurrentView.Should().Be(NavigationView.History);
        _viewModel.IsHistoryActive.Should().BeTrue();
        _viewModel.IsDashboardActive.Should().BeFalse();
    }

    [Fact]
    public void OpenSettingsCommand_SwitchesViewAndUpdatesActiveFlags()
    {
        _viewModel.OpenSettingsCommand.Execute(null);

        _viewModel.CurrentView.Should().Be(NavigationView.Settings);
        _viewModel.IsSettingsActive.Should().BeTrue();
        _viewModel.IsDashboardActive.Should().BeFalse();
    }

    [Fact]
    public async Task NavigateToDashboardCommand_SwitchesViewAndUpdatesActiveFlags()
    {
        _viewModel.NavigateToFoldersCommand.Execute(null);
        await _viewModel.NavigateToDashboardCommand.ExecuteAsync(null);

        _viewModel.CurrentView.Should().Be(NavigationView.Dashboard);
        _viewModel.IsDashboardActive.Should().BeTrue();
        _viewModel.IsFoldersActive.Should().BeFalse();
    }

    [Fact]
    public void NavigateToFolders_WhenRuleIsDirtyAndUserRejectsDiscard_CancelsNavigation()
    {
        _viewModel.SelectedRule = new Rule { Name = "Dirty Rule" };
        _viewModel.IsRuleDirty = true;
        _viewModel.ConfirmDiscardRuleCallback = (msg, caption) => false;

        _viewModel.NavigateToFoldersCommand.Execute(null);

        _viewModel.CurrentView.Should().Be(NavigationView.Dashboard);
        _viewModel.IsFoldersActive.Should().BeFalse();
    }
}
