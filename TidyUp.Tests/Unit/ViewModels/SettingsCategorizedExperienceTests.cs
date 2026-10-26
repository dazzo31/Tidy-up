using System.IO;
using FluentAssertions;
using Moq;
using TidyUp.Data;
using TidyUp.Data.Recovery;
using TidyUp.Models;
using TidyUp.Models.Enums;
using TidyUp.Models.Domain;
using TidyUp.ViewModels;
using TidyUp.Configuration;
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

        _settingsRepo = new SettingsRepository(new TidyUp.Configuration.TidyUpPaths(_tempTestDir));
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
        settings.MaxConcurrentOperations.Should().Be(1);
        settings.ScanBatchSize.Should().Be(500);
        settings.DebounceDelayMs.Should().Be(500);
        settings.NetworkFolderPollingIntervalSeconds.Should().Be(30);
        settings.FileLockRetryCount.Should().Be(3);
        settings.LogRetentionDays.Should().Be(30);
        settings.AutoCleanupLogs.Should().BeTrue();
        settings.DebugLoggingEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task VacuumDatabaseCommand_WhenSpaceReclaimed_ReportsReclaimedBytesAndDuration()
    {
        var paths = new TidyUpPaths(_tempTestDir);
        File.WriteAllText(paths.DatabasePath, "placeholder");

        var integrityCheckerMock = new Mock<TidyUp.Data.Recovery.IDbIntegrityChecker>();
        integrityCheckerMock
            .Setup(c => c.VacuumDatabaseAsync(paths.DatabasePath))
            .ReturnsAsync(new TidyUp.Data.Recovery.DatabaseMaintenanceResult
            {
                Success = true,
                PreSizeBytes = 1048576,
                PostSizeBytes = 524288,
                Duration = TimeSpan.FromMilliseconds(45)
            });


        var vm = new SettingsViewModel(_settingsRepo, _backupManagerMock.Object, integrityCheckerMock.Object, paths);

        await vm.VacuumDatabaseCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("Database optimized successfully. Reclaimed 512.0 KB");
        vm.StatusMessage.Should().Contain("45 ms");
    }

    [Fact]
    public async Task VacuumDatabaseCommand_WhenAlreadyCompact_ReportsZeroBytesReclaimed()
    {
        var paths = new TidyUpPaths(_tempTestDir);
        File.WriteAllText(paths.DatabasePath, "placeholder");

        var integrityCheckerMock = new Mock<TidyUp.Data.Recovery.IDbIntegrityChecker>();
        integrityCheckerMock
            .Setup(c => c.VacuumDatabaseAsync(paths.DatabasePath))
            .ReturnsAsync(new TidyUp.Data.Recovery.DatabaseMaintenanceResult
            {
                Success = true,
                PreSizeBytes = 524288,
                PostSizeBytes = 524288,
                Duration = TimeSpan.FromMilliseconds(20)
            });


        var vm = new SettingsViewModel(_settingsRepo, _backupManagerMock.Object, integrityCheckerMock.Object, paths);

        await vm.VacuumDatabaseCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("Database already compact (0 bytes reclaimed)");
    }

    [Fact]
    public async Task DbIntegrityChecker_VacuumDatabaseAsync_ExecutesPragmaAndVacuumsRealDatabase()
    {
        var tempDb = Path.Combine(_tempTestDir, "real_vacuum.db");
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={tempDb}"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE TestTable (Id INTEGER PRIMARY KEY, Data TEXT);";
            await cmd.ExecuteNonQueryAsync();

            // Insert 2000 rows to expand database size
            using var tx = conn.BeginTransaction();
            cmd.Transaction = tx;
            for (int i = 0; i < 2000; i++)
            {
                cmd.CommandText = $"INSERT INTO TestTable (Data) VALUES ('Large block of padding data for testing vacuum space reclamation {i}');";
                await cmd.ExecuteNonQueryAsync();
            }
            tx.Commit();
            cmd.Transaction = null;

            // Delete all rows to leave free pages that VACUUM will reclaim
            cmd.CommandText = "DELETE FROM TestTable;";
            await cmd.ExecuteNonQueryAsync();
            await conn.CloseAsync();
        }

        var checker = new TidyUp.Data.Recovery.DbIntegrityChecker(_backupManagerMock.Object);
        var result = await checker.VacuumDatabaseAsync(tempDb);

        result.Success.Should().BeTrue();
        result.PreSizeBytes.Should().BeGreaterThan(0);
        result.PostSizeBytes.Should().BeLessThan(result.PreSizeBytes);
        result.ReclaimedBytes.Should().BeGreaterThan(0);
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

    [Fact]
    public void BrowseBackupDirectoryCommand_WhenFolderSelected_UpdatesSettingsDatabaseBackupPath()
    {
        var paths = new TidyUpPaths(_tempTestDir);
        var vm = new SettingsViewModel(_settingsRepo, _backupManagerMock.Object, null, paths);
        var customPath = Path.Combine(_tempTestDir, "CustomBackups");
        vm.BrowseFolderHandler = () => customPath;

        vm.BrowseBackupDirectoryCommand.Execute(null);

        vm.Settings.DatabaseBackupPath.Should().Be(customPath);
        vm.DefaultBackupsDirectory.Should().Be(paths.BackupsDirectory);
    }

    [Fact]
    public void ActionEditorViewModel_UsesAppSettingsDefaults_ForConflictResolutionAndRecycleBin()
    {
        var settings = new AppSettings
        {
            DefaultConflictResolution = ConflictResolution.Overwrite,
            UseRecycleBinByDefault = false
        };

        var actionEditor = new ActionEditorViewModel(settings);

        actionEditor.AddMoveActionCommand.Execute(null);
        actionEditor.SelectedAction.Should().BeOfType<MoveFileAction>();
        ((MoveFileAction)actionEditor.SelectedAction!).ConflictResolution.Should().Be(ConflictResolution.Overwrite);

        actionEditor.AddCopyActionCommand.Execute(null);
        actionEditor.SelectedAction.Should().BeOfType<CopyFileAction>();
        ((CopyFileAction)actionEditor.SelectedAction!).ConflictResolution.Should().Be(ConflictResolution.Overwrite);

        actionEditor.AddDeleteActionCommand.Execute(null);
        actionEditor.SelectedAction.Should().BeOfType<DeleteFileAction>();
        ((DeleteFileAction)actionEditor.SelectedAction!).UseRecycleBin.Should().BeFalse();
    }

    [Fact]
    public async Task DatabaseBackupManager_HonorsCustomBackupPath_WhenSpecifiedInAppSettings()
    {
        var customBackupDir = Path.Combine(_tempTestDir, "CustomBackupDir");
        Directory.CreateDirectory(customBackupDir);

        var dbPath = Path.Combine(_tempTestDir, "source.db");
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE T (Id INT);";
            await cmd.ExecuteNonQueryAsync();
            await conn.CloseAsync();
        }

        var settings = new AppSettings { DatabaseBackupPath = customBackupDir };
        var manager = new DatabaseBackupManager(dbPath, Path.Combine(_tempTestDir, "DefaultBackups"), settings);

        var backupPath = await manager.CreateBackupAsync(tag: "custom_test");

        backupPath.Should().NotBeNull();
        backupPath!.Should().StartWith(customBackupDir);
        File.Exists(backupPath).Should().BeTrue();
    }
}
