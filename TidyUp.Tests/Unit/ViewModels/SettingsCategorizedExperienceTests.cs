using System.IO;
using FluentAssertions;
using Moq;
using TidyUp.Data;
using TidyUp.Data.Recovery;
using TidyUp.Models;
using TidyUp.Models.Enums;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class SettingsCategorizedExperienceTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly SettingsRepository _settingsRepo;
    private readonly Mock<IDatabaseBackupManager> _backupManagerMock;

    public SettingsCategorizedExperienceTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), $"TidyUp_SettingsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempTestDir);

        _settingsRepo = new SettingsRepository();
        _backupManagerMock = new Mock<IDatabaseBackupManager>();
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
    public void InitialState_DefaultsToGeneralCategory()
    {
        var vm = new SettingsViewModel(_settingsRepo, _backupManagerMock.Object);

        vm.SelectedCategory.Should().Be(SettingsViewModel.SettingsCategory.General);
        vm.IsGeneralActive.Should().BeTrue();
        vm.IsSafetyActive.Should().BeFalse();
        vm.IsPerformanceActive.Should().BeFalse();
        vm.IsStorageLogsActive.Should().BeFalse();
        vm.IsAdvancedActive.Should().BeFalse();
    }

    [Fact]
    public void SelectCategory_SwitchesActiveSectionAndUpdatesProperties()
    {
        var vm = new SettingsViewModel(_settingsRepo, _backupManagerMock.Object);

        vm.SelectCategory(SettingsViewModel.SettingsCategory.Safety);
        vm.SelectedCategory.Should().Be(SettingsViewModel.SettingsCategory.Safety);
        vm.IsSafetyActive.Should().BeTrue();
        vm.IsGeneralActive.Should().BeFalse();

        vm.SelectCategory(SettingsViewModel.SettingsCategory.Performance);
        vm.IsPerformanceActive.Should().BeTrue();
        vm.IsSafetyActive.Should().BeFalse();

        vm.SelectCategory(SettingsViewModel.SettingsCategory.StorageAndLogs);
        vm.IsStorageLogsActive.Should().BeTrue();

        vm.SelectCategory(SettingsViewModel.SettingsCategory.Advanced);
        vm.IsAdvancedActive.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_DefaultValues_HaveSafeAndSensibleDefaults()
    {
        var settings = AppSettings.CreateDefault();

        settings.UseRecycleBinByDefault.Should().BeTrue();
        settings.RequireDestructiveConfirmations.Should().BeTrue();
        settings.DefaultConflictResolution.Should().Be(ConflictResolution.RenameNew);
        settings.MaxConcurrentOperations.Should().Be(4);
        settings.ScanBatchSize.Should().Be(500);
        settings.DebounceDelayMs.Should().Be(500);
        settings.NetworkFolderPollingIntervalSeconds.Should().Be(30);
        settings.FileLockRetryCount.Should().Be(3);
        settings.LogRetentionDays.Should().Be(30);
        settings.AutoCleanupLogs.Should().BeTrue();
        settings.DebugLoggingEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task VacuumDatabaseCommand_ExecutesAndUpdatesStatus()
    {
        var vm = new SettingsViewModel(_settingsRepo, _backupManagerMock.Object);

        await vm.VacuumDatabaseCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("Database optimization completed");
    }

    [Fact]
    public async Task BackupDatabaseCommand_WhenManagerPresent_CallsCreateBackup()
    {
        _backupManagerMock
            .Setup(m => m.CreateBackupAsync(null, null, "manual"))
            .ReturnsAsync("C:\\Backups\\backup_manual.db");

        var vm = new SettingsViewModel(_settingsRepo, _backupManagerMock.Object);

        await vm.BackupDatabaseCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("Database backup created");
        _backupManagerMock.Verify(m => m.CreateBackupAsync(null, null, "manual"), Times.Once);
    }
}
