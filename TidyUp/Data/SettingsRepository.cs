using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TidyUp.Configuration;
using TidyUp.Models;

namespace TidyUp.Data;

public class SettingsRepository
{
    private readonly TidyUpPaths _paths;
    private AppSettings? _shared;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsRepository(TidyUpPaths paths)
    {
        _paths = paths;
    }

    private string SettingsDirectory => _paths.AppDataRoot;
    private string SettingsFilePath => _paths.SettingsPath;

    private AppSettings WithResolvedDefaults(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DatabaseBackupPath))
        {
            settings.DatabaseBackupPath = _paths.BackupsDirectory;
        }

        if (settings.SchemaVersion < 2)
        {
            // Theme was persisted but never applied before v2, so a stored "Dark" is just the old default,
            // not a choice anyone saw take effect. Don't flip existing installs to dark on upgrade.
            if (string.Equals(settings.Theme, AppThemes.Dark, StringComparison.OrdinalIgnoreCase))
            {
                settings.Theme = AppThemes.System;
            }

            settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        }

        settings.Theme = AppThemes.Normalize(settings.Theme);
        return settings;
    }

    /// <summary>
    /// The single app-wide settings instance. Every <see cref="SaveAsync"/> is mirrored into it, so code that
    /// reads or re-saves it (window geometry, tray, theme) never works from a stale startup copy.
    /// </summary>
    public async Task<AppSettings> LoadSharedAsync()
    {
        return _shared ??= await LoadAsync();
    }

    /// <summary>
    /// Loads settings from disk, or creates default settings if file doesn't exist
    /// </summary>
    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return WithResolvedDefaults(AppSettings.CreateDefault());
            }

            var json = await File.ReadAllTextAsync(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return WithResolvedDefaults(settings ?? AppSettings.CreateDefault());
        }
        catch (Exception ex)
        {
            // Log error and return defaults
            Console.WriteLine($"Error loading settings: {ex.Message}");
            return WithResolvedDefaults(AppSettings.CreateDefault());
        }
    }

    /// <summary>
    /// Saves settings to disk
    /// </summary>
    public async Task SaveAsync(AppSettings settings)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(SettingsDirectory);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(SettingsFilePath, json);

            if (_shared != null && !ReferenceEquals(_shared, settings))
            {
                _shared.CopyFrom(settings);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Checks if settings file exists (used for first-run detection)
    /// </summary>
    public bool SettingsFileExists()
    {
        return File.Exists(SettingsFilePath);
    }

    /// <summary>
    /// Deletes settings file and resets to defaults
    /// </summary>
    public async Task ResetToDefaultsAsync()
    {
        if (File.Exists(SettingsFilePath))
        {
            File.Delete(SettingsFilePath);
        }

        var defaults = AppSettings.CreateDefault();
        await SaveAsync(defaults);
    }
}
