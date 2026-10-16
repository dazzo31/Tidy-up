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

        return settings;
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
