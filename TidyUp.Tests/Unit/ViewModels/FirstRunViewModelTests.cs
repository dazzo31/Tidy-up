using FluentAssertions;
using Moq;
using TidyUp.Data;
using TidyUp.Data.Repositories;
using TidyUp.Models;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Tests.TestInfrastructure;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public sealed class FirstRunViewModelTests : IDisposable
{
    private readonly IsolatedAppData _appData = new();
    private readonly SettingsRepository _settingsRepo;
    private readonly Mock<IRuleRepository> _rules = new();
    private readonly AppSettings _settings = AppSettings.CreateDefault();
    private readonly List<Rule> _existingRules = [];
    private readonly List<Rule> _added = [];

    public FirstRunViewModelTests()
    {
        _settingsRepo = new SettingsRepository(_appData.Paths);
        _rules.Setup(r => r.GetAllAsync()).ReturnsAsync(() => _existingRules);
        _rules.Setup(r => r.AddAsync(It.IsAny<Rule>())).ReturnsAsync((Rule r) =>
        {
            _added.Add(r);
            return r;
        });
    }

    public void Dispose() => _appData.Dispose();

    private FirstRunViewModel Create() => new(_settingsRepo, _rules.Object, _settings);

    private async Task<AppSettings> PersistedAsync() => await _settingsRepo.LoadAsync();

    [Fact]
    public void ShowThisAgain_DefaultsToOff_EvenThoughTheWizardIsEnabled()
    {
        // Regression: it used to mirror ShowFirstRunWizard (true), so a normal Finish re-showed the wizard every launch.
        _settings.ShowFirstRunWizard = true;

        Create().ShowThisAgain.Should().BeFalse();
    }

    [Fact]
    public async Task Finish_AppliesChoices_PersistsThatTheWizardWasSeen_AndCreatesADisabledSampleRule()
    {
        var vm = Create();
        vm.EnableSystemTray = false;
        vm.EnableNotifications = false;
        vm.SelectedConflictStrategy = ConflictResolution.Overwrite;
        var closed = false;
        vm.OnFinishRequested = () => closed = true;

        await vm.FinishCommand.ExecuteAsync(null);

        var saved = await PersistedAsync();
        saved.ShowFirstRunWizard.Should().BeFalse();
        saved.SystemTrayEnabled.Should().BeFalse();
        saved.ToastNotificationsEnabled.Should().BeFalse();
        saved.DefaultConflictResolution.Should().Be(ConflictResolution.Overwrite);
        vm.WasCompleted.Should().BeTrue();
        closed.Should().BeTrue();

        var sample = _added.Should().ContainSingle().Subject;
        sample.Name.Should().Be(FirstRunViewModel.SampleRuleName);
        sample.IsEnabled.Should().BeFalse("a sample rule must never start moving files before the user has reviewed it");
    }

    [Fact]
    public async Task Finish_WithShowThisAgainTicked_KeepsTheWizardEnabledForNextStartup()
    {
        var vm = Create();
        vm.ShowThisAgain = true;

        await vm.FinishCommand.ExecuteAsync(null);

        (await PersistedAsync()).ShowFirstRunWizard.Should().BeTrue();
    }

    [Fact]
    public async Task Finish_WithoutTheSampleRuleOption_CreatesNoRule()
    {
        var vm = Create();
        vm.CreateSampleRule = false;

        await vm.FinishCommand.ExecuteAsync(null);

        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Finish_WhenTheSampleRuleAlreadyExists_DoesNotCreateADuplicate()
    {
        _existingRules.Add(new Rule { Name = FirstRunViewModel.SampleRuleName });

        await Create().FinishCommand.ExecuteAsync(null);

        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Skip_LeavesEveryChoiceUnappliedAndCreatesNothing_ButCountsAsSeen()
    {
        // Regression: Skip was bound to FinishCommand, so skipping applied the choices and created the sample rule.
        var vm = Create();
        vm.EnableSystemTray = false;
        vm.EnableNotifications = false;
        vm.SelectedConflictStrategy = ConflictResolution.Overwrite;
        var closed = false;
        vm.OnFinishRequested = () => closed = true;

        await vm.SkipCommand.ExecuteAsync(null);

        var saved = await PersistedAsync();
        saved.ShowFirstRunWizard.Should().BeFalse();
        saved.SystemTrayEnabled.Should().BeTrue();
        saved.ToastNotificationsEnabled.Should().BeTrue();
        saved.DefaultConflictResolution.Should().NotBe(ConflictResolution.Overwrite);
        _added.Should().BeEmpty();
        vm.WasCompleted.Should().BeFalse();
        closed.Should().BeTrue();
    }

    [Fact]
    public async Task ClosingTheWindowWithoutFinishing_StillCountsAsSeen()
    {
        var vm = Create();

        await vm.EnsureResolvedAsync();

        (await PersistedAsync()).ShowFirstRunWizard.Should().BeFalse();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureResolved_AfterFinish_DoesNotOverrideTheUsersShowAgainChoice()
    {
        var vm = Create();
        vm.ShowThisAgain = true;
        await vm.FinishCommand.ExecuteAsync(null);

        await vm.EnsureResolvedAsync();

        (await PersistedAsync()).ShowFirstRunWizard.Should().BeTrue();
    }

    [Fact]
    public async Task Resolving_UpdatesTheSharedInstanceThatTheRestOfTheAppReads()
    {
        var shared = await _settingsRepo.LoadSharedAsync();
        shared.ShowFirstRunWizard = true;
        var vm = new FirstRunViewModel(_settingsRepo, _rules.Object, shared);

        await vm.SkipCommand.ExecuteAsync(null);

        shared.ShowFirstRunWizard.Should().BeFalse();
    }

    [Fact]
    public void Navigation_StaysWithinTheFiveSteps()
    {
        var vm = Create();

        vm.PreviousCommand.Execute(null);
        vm.CurrentStep.Should().Be(1);

        for (var i = 0; i < 10; i++) vm.NextCommand.Execute(null);
        vm.CurrentStep.Should().Be(5);
    }
}
