using FluentAssertions;
using TidyUp.Data;
using TidyUp.Models;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Data;

public sealed class SettingsPersistenceTests : IDisposable
{
    private readonly IsolatedAppData _appData = new();
    private readonly SettingsRepository _repo;

    public SettingsPersistenceTests()
    {
        _repo = new SettingsRepository(_appData.Paths);
    }

    public void Dispose() => _appData.Dispose();

    private async Task WriteSettingsFileAsync(string json)
    {
        Directory.CreateDirectory(_appData.Paths.AppDataRoot);
        await File.WriteAllTextAsync(_appData.Paths.SettingsPath, json);
    }

    [Fact]
    public async Task Load_LegacyFileWithDefaultDark_DoesNotFlipTheInstallToDark()
    {
        // Shape of a real v1.1.0 settings.json: no SchemaVersion, and "Dark" is only the old default.
        await WriteSettingsFileAsync("""{ "Theme": "Dark", "ScanBatchSize": 777, "MinimizeToTray": false }""");

        var settings = await _repo.LoadAsync();

        settings.Theme.Should().Be("System");
        settings.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
        settings.ScanBatchSize.Should().Be(777, "migration must not discard any other stored value");
        settings.MinimizeToTray.Should().BeFalse();
    }

    [Fact]
    public async Task Load_LegacyFileWithExplicitLight_KeepsLight()
    {
        await WriteSettingsFileAsync("""{ "Theme": "Light" }""");

        (await _repo.LoadAsync()).Theme.Should().Be("Light");
    }

    [Fact]
    public async Task Load_CurrentVersionFileWithDark_KeepsDark()
    {
        await WriteSettingsFileAsync("""{ "SchemaVersion": 2, "Theme": "Dark" }""");

        (await _repo.LoadAsync()).Theme.Should().Be("Dark");
    }

    [Theory]
    [InlineData("dark", "Dark")]
    [InlineData(" LIGHT ", "Light")]
    [InlineData("Purple", "System")]
    [InlineData("", "System")]
    public async Task Load_NormalisesTheThemeSpelling(string stored, string expected)
    {
        await WriteSettingsFileAsync($$"""{ "SchemaVersion": 2, "Theme": "{{stored}}" }""");

        (await _repo.LoadAsync()).Theme.Should().Be(expected);
    }

    [Fact]
    public async Task Load_WithoutAFile_ReturnsCurrentDefaults()
    {
        var settings = await _repo.LoadAsync();

        settings.Theme.Should().Be("System");
        settings.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
    }

    [Fact]
    public async Task LoadShared_ReturnsOneInstanceForTheLifetimeOfTheRepository()
    {
        var first = await _repo.LoadSharedAsync();
        var second = await _repo.LoadSharedAsync();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task Save_FromAnotherCopy_IsMirroredIntoTheSharedInstance_AndNotifies()
    {
        var shared = await _repo.LoadSharedAsync();
        var raised = new List<string?>();
        shared.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        var editorCopy = await _repo.LoadAsync();
        editorCopy.Theme = "Dark";
        editorCopy.SystemTrayEnabled = false;
        await _repo.SaveAsync(editorCopy);

        shared.Theme.Should().Be("Dark");
        shared.SystemTrayEnabled.Should().BeFalse();
        raised.Should().Contain(nameof(AppSettings.Theme));
    }

    [Fact]
    public async Task WindowGeometrySave_AfterASettingsChange_DoesNotRevertThatChange()
    {
        // Regression: Settings edited and saved one copy, while the window saved a stale startup copy on close,
        // so every change made in Settings was reverted when the app was closed.
        var startupCopy = await _repo.LoadSharedAsync();

        var editorCopy = await _repo.LoadAsync();
        editorCopy.UseRecycleBinByDefault = false;
        editorCopy.Theme = "Dark";
        await _repo.SaveAsync(editorCopy);

        startupCopy.WindowWidth = 1234;
        await _repo.SaveAsync(startupCopy);

        var onDisk = await _repo.LoadAsync();
        onDisk.UseRecycleBinByDefault.Should().BeFalse();
        onDisk.Theme.Should().Be("Dark");
        onDisk.WindowWidth.Should().Be(1234);
    }

    [Fact]
    public async Task Save_ThenLoad_RoundTripsTheSchemaVersionWithoutRemigrating()
    {
        var settings = await _repo.LoadAsync();
        settings.Theme = "Dark";
        await _repo.SaveAsync(settings);

        var reloaded = await _repo.LoadAsync();

        reloaded.Theme.Should().Be("Dark", "a deliberate Dark choice made after migration must survive the next launch");
        reloaded.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
    }

    [Fact]
    public void CopyFrom_CopiesEveryWritableProperty()
    {
        var source = new AppSettings
        {
            Theme = "Dark", ScanBatchSize = 9, MinimizeToTray = false, WindowWidth = 5, DatabaseBackupPath = "x", SchemaVersion = 7
        };

        var target = new AppSettings();
        target.CopyFrom(source);

        target.Should().BeEquivalentTo(source);
    }

    [Fact]
    public async Task SaveAsync_FirstSave_CreatesFileAndKnownGoodBackup()
    {
        var settings = new AppSettings { Theme = "Dark", ScanBatchSize = 250 };

        await _repo.SaveAsync(settings);

        File.Exists(_appData.Paths.SettingsPath).Should().BeTrue();
        var backupPath = _appData.Paths.SettingsPath + ".bak";
        File.Exists(backupPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_SubsequentSave_AtomicallyUpdatesFileAndPreservesPreviousInBackup()
    {
        var v1 = new AppSettings { Theme = "Light", ScanBatchSize = 100 };
        await _repo.SaveAsync(v1);

        var v2 = new AppSettings { Theme = "Dark", ScanBatchSize = 500 };
        await _repo.SaveAsync(v2);

        var currentJson = await File.ReadAllTextAsync(_appData.Paths.SettingsPath);
        currentJson.Should().Contain("Dark");
        currentJson.Should().Contain("500");

        var backupPath = _appData.Paths.SettingsPath + ".bak";
        File.Exists(backupPath).Should().BeTrue();
        var backupJson = await File.ReadAllTextAsync(backupPath);
        backupJson.Should().Contain("Light");
        backupJson.Should().Contain("100");
    }

    [Fact]
    public async Task SaveAsync_CleansAbandonedTempFiles()
    {
        Directory.CreateDirectory(_appData.Paths.AppDataRoot);
        var abandonedTemp = Path.Combine(_appData.Paths.AppDataRoot, "settings.abandoned.tmp");
        await File.WriteAllTextAsync(abandonedTemp, "abandoned temporary content");

        var settings = new AppSettings { Theme = "System" };
        await _repo.SaveAsync(settings);

        File.Exists(abandonedTemp).Should().BeFalse("abandoned temporary files must be cleaned up");
    }

    [Fact]
    public async Task SaveAsync_ConcurrentSaves_AreSynchronizedWithoutCorruption()
    {
        var tasks = Enumerable.Range(1, 20).Select(async i =>
        {
            var s = new AppSettings { WindowWidth = 800 + i, ScanBatchSize = 100 + i };
            await _repo.SaveAsync(s);
        });

        await Task.WhenAll(tasks);

        var loaded = await _repo.LoadAsync();
        loaded.Should().NotBeNull();
        loaded.WindowWidth.Should().BeInRange(801, 820);
    }

    [Fact]
    public async Task LoadWithResultAsync_WhenFileCorrupt_PreservesCorruptFileWithTimestamp()
    {
        var corruptData = "{ broken json content !!! not valid json }";
        await WriteSettingsFileAsync(corruptData);

        var result = await _repo.LoadWithResultAsync();

        result.IsCorrupt.Should().BeTrue();
        result.RecoveredFromBackup.Should().BeFalse();
        result.CorruptBackupPath.Should().NotBeNull();
        File.Exists(result.CorruptBackupPath).Should().BeTrue();

        var preservedBytes = await File.ReadAllTextAsync(result.CorruptBackupPath!);
        preservedBytes.Should().Be(corruptData, "corrupt file contents must be preserved byte-for-byte");
    }

    [Fact]
    public async Task LoadWithResultAsync_WhenFileCorruptAndBackupExists_RestoresFromBackup()
    {
        Directory.CreateDirectory(_appData.Paths.AppDataRoot);
        var backupPath = _appData.Paths.SettingsPath + ".bak";
        var goodBackupJson = """{ "SchemaVersion": 2, "Theme": "Dark", "ScanBatchSize": 333 }""";
        await File.WriteAllTextAsync(backupPath, goodBackupJson);

        var corruptData = "{ corrupt: true, unclosed json";
        await WriteSettingsFileAsync(corruptData);

        var result = await _repo.LoadWithResultAsync();

        result.IsCorrupt.Should().BeTrue();
        result.RecoveredFromBackup.Should().BeTrue();
        result.Settings.Theme.Should().Be("Dark");
        result.Settings.ScanBatchSize.Should().Be(333);
        result.CorruptBackupPath.Should().NotBeNull();
        File.Exists(result.CorruptBackupPath).Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_SemanticValidation_NormalizesInvalidScanBatchSize()
    {
        await WriteSettingsFileAsync("""{ "SchemaVersion": 2, "ScanBatchSize": -99 }""");

        var loaded = await _repo.LoadAsync();

        loaded.ScanBatchSize.Should().Be(100, "invalid non-positive batch size must be normalized to safe default");
    }
}
