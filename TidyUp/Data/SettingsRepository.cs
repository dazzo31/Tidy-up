using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TidyUp.Configuration;
using TidyUp.Models;

namespace TidyUp.Data;

/// <summary>
/// Detailed result of a settings load operation, including corruption detection and backup recovery state.
/// </summary>
public class SettingsLoadResult
{
    public AppSettings Settings { get; init; } = null!;
    public bool IsCorrupt { get; init; }
    public bool RecoveredFromBackup { get; init; }
    public string? CorruptBackupPath { get; init; }
    public string? BackupPathUsed { get; init; }
    public string? ErrorMessage { get; init; }
}

public class SettingsRepository
{
    private readonly TidyUpPaths _paths;
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
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
    private string SettingsBackupPath => _paths.SettingsPath + ".bak";

    /// <summary>
    /// Gets the result of the most recent load operation.
    /// </summary>
    public SettingsLoadResult? LastLoadResult { get; private set; }

    private AppSettings WithResolvedDefaults(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DatabaseBackupPath))
        {
            settings.DatabaseBackupPath = _paths.BackupsDirectory;
        }

        if (settings.ScanBatchSize <= 0)
        {
            settings.ScanBatchSize = 100;
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
        if (_shared == null)
        {
            var result = await LoadWithResultAsync();
            _shared = result.Settings;
        }
        return _shared;
    }

    /// <summary>
    /// Synchronously retrieves the shared settings singleton, loading from disk on first access if needed.
    /// </summary>
    public AppSettings CurrentSettings
    {
        get
        {
            if (_shared == null)
            {
                var result = LoadWithResultAsync().GetAwaiter().GetResult();
                _shared = result.Settings;
            }
            return _shared;
        }
    }

    /// <summary>
    /// Explicitly sets or updates the shared singleton settings instance.
    /// </summary>
    public void SetShared(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (_shared == null)
        {
            _shared = settings;
        }
        else if (!ReferenceEquals(_shared, settings))
        {
            _shared.CopyFrom(settings);
        }
    }

    /// <summary>
    /// Loads settings from disk with structured corruption detection and backup recovery.
    /// </summary>
    public async Task<SettingsLoadResult> LoadWithResultAsync()
    {
        if (!File.Exists(SettingsFilePath))
        {
            var cleanResult = new SettingsLoadResult
            {
                Settings = WithResolvedDefaults(AppSettings.CreateDefault()),
                IsCorrupt = false,
                RecoveredFromBackup = false
            };
            LastLoadResult = cleanResult;
            return cleanResult;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(SettingsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new JsonException("Settings file is empty or whitespace.");
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings == null)
            {
                throw new JsonException("Deserialized settings object is null.");
            }

            var resolved = WithResolvedDefaults(settings);

            // Ensure known-good backup exists
            EnsureKnownGoodBackup();

            var successResult = new SettingsLoadResult
            {
                Settings = resolved,
                IsCorrupt = false,
                RecoveredFromBackup = false
            };
            LastLoadResult = successResult;
            return successResult;
        }
        catch (Exception ex)
        {
            // Profile is corrupt or invalid JSON! Preserve corrupt bytes with timestamped backup
            var corruptTimestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var corruptBackupPath = $"{SettingsFilePath}.corrupt.{corruptTimestamp}";

            try
            {
                File.Copy(SettingsFilePath, corruptBackupPath, overwrite: true);
            }
            catch (Exception copyEx)
            {
                Console.WriteLine($"Failed to preserve corrupt settings file: {copyEx.Message}");
            }

            // Attempt recovery from known-good backup
            if (File.Exists(SettingsBackupPath))
            {
                try
                {
                    var backupJson = await File.ReadAllTextAsync(SettingsBackupPath);
                    var backupSettings = JsonSerializer.Deserialize<AppSettings>(backupJson, JsonOptions);
                    if (backupSettings != null)
                    {
                        var resolvedBackup = WithResolvedDefaults(backupSettings);

                        // Restore backup to main settings file
                        File.Copy(SettingsBackupPath, SettingsFilePath, overwrite: true);

                        var recoveryResult = new SettingsLoadResult
                        {
                            Settings = resolvedBackup,
                            IsCorrupt = true,
                            RecoveredFromBackup = true,
                            CorruptBackupPath = corruptBackupPath,
                            BackupPathUsed = SettingsBackupPath,
                            ErrorMessage = ex.Message
                        };
                        LastLoadResult = recoveryResult;
                        return recoveryResult;
                    }
                }
                catch (Exception backupEx)
                {
                    Console.WriteLine($"Backup settings file was also invalid: {backupEx.Message}");
                }
            }

            // Could not recover from backup: return defaults with corruption flag
            var fallbackResult = new SettingsLoadResult
            {
                Settings = WithResolvedDefaults(AppSettings.CreateDefault()),
                IsCorrupt = true,
                RecoveredFromBackup = false,
                CorruptBackupPath = corruptBackupPath,
                ErrorMessage = ex.Message
            };
            LastLoadResult = fallbackResult;
            return fallbackResult;
        }
    }

    /// <summary>
    /// Loads settings from disk, or creates default settings if file doesn't exist
    /// </summary>
    public async Task<AppSettings> LoadAsync()
    {
        var result = await LoadWithResultAsync();
        return result.Settings;
    }

    /// <summary>
    /// Saves settings to disk atomically using temporary file replacement and backup preservation.
    /// Concurrent save calls are synchronized via semaphore.
    /// </summary>
    public async Task SaveAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _saveSemaphore.WaitAsync();
        try
        {
            Directory.CreateDirectory(SettingsDirectory);

            // Clean up any abandoned temporary files from previous interrupted runs
            CleanStrayTempFiles();

            var tempPath = Path.Combine(SettingsDirectory, $"settings.{Guid.NewGuid():N}.tmp");

            try
            {
                // Write validated JSON to temporary file and flush
                await using (var fileStream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.WriteThrough | FileOptions.Asynchronous))
                {
                    await JsonSerializer.SerializeAsync(fileStream, settings, JsonOptions);
                    await fileStream.FlushAsync();
                }

                // Atomic replacement: if target does not exist, move into place; otherwise use File.Replace
                if (!File.Exists(SettingsFilePath))
                {
                    File.Move(tempPath, SettingsFilePath);
                    File.Copy(SettingsFilePath, SettingsBackupPath, overwrite: true);
                }
                else
                {
                    File.Replace(tempPath, SettingsFilePath, SettingsBackupPath, ignoreMetadataErrors: true);
                }

                if (_shared != null && !ReferenceEquals(_shared, settings))
                {
                    _shared.CopyFrom(settings);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    private void EnsureKnownGoodBackup()
    {
        try
        {
            if (File.Exists(SettingsFilePath) && !File.Exists(SettingsBackupPath))
            {
                File.Copy(SettingsFilePath, SettingsBackupPath, overwrite: true);
            }
        }
        catch
        {
            // Non-critical background task
        }
    }

    private void CleanStrayTempFiles()
    {
        try
        {
            var tempFiles = Directory.GetFiles(SettingsDirectory, "settings.*.tmp");
            foreach (var file in tempFiles)
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch
        {
            // Non-critical background cleanup
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
