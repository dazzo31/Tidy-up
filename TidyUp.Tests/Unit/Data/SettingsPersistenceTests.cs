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
}
