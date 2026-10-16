using FluentAssertions;
using Moq;
using TidyUp.Data;
using TidyUp.Tests.TestInfrastructure;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public sealed class SettingsSetupWizardTests : IDisposable
{
    private readonly IsolatedAppData _appData = new();

    public void Dispose() => _appData.Dispose();

    [Fact]
    public async Task RunSetupWizard_WithoutAHandler_ExplainsInsteadOfFailing()
    {
        var vm = new SettingsViewModel(new SettingsRepository(_appData.Paths));

        await vm.RunSetupWizardCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("not available");
    }

    [Fact]
    public async Task RunSetupWizard_RefreshesTheEditorCopy_SoALaterSaveCannotRevertTheWizardsChoices()
    {
        var repo = new SettingsRepository(_appData.Paths);
        var vm = new SettingsViewModel(repo, Mock.Of<TidyUp.Data.Recovery.IDatabaseBackupManager>());
        await vm.LoadSettingsCommand.ExecuteAsync(null);
        vm.Settings.SystemTrayEnabled.Should().BeTrue();

        vm.RunSetupWizardHandler = async () =>
        {
            var changedByWizard = await repo.LoadAsync();
            changedByWizard.SystemTrayEnabled = false;
            await repo.SaveAsync(changedByWizard);
        };

        await vm.RunSetupWizardCommand.ExecuteAsync(null);
        vm.Settings.SystemTrayEnabled.Should().BeFalse("the editor must show what the wizard saved");

        await vm.SaveSettingsCommand.ExecuteAsync(null);
        (await repo.LoadAsync()).SystemTrayEnabled.Should().BeFalse();
    }
}
