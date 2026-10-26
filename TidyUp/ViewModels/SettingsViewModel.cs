using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Configuration;
using TidyUp.Data;
using TidyUp.Models;
using TidyUp.Models.Enums;
using TidyUp.Services.Diagnostics;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for application settings.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsRepository _settingsRepository;
    private readonly TidyUp.Data.Recovery.IDatabaseBackupManager? _backupManager;
    private readonly TidyUp.Data.Recovery.IDbIntegrityChecker? _integrityChecker;
    private readonly TidyUp.Configuration.TidyUpPaths? _paths;

    public enum SettingsCategory
    {
        General,
        Safety,
        Performance,
        StorageAndLogs,
        Advanced
    }

    [ObservableProperty]
    private SettingsCategory _selectedCategory = SettingsCategory.General;

    public bool IsGeneralActive => SelectedCategory == SettingsCategory.General;
    public bool IsSafetyActive => SelectedCategory == SettingsCategory.Safety;
    public bool IsPerformanceActive => SelectedCategory == SettingsCategory.Performance;
    public bool IsStorageLogsActive => SelectedCategory == SettingsCategory.StorageAndLogs;
    public bool IsAdvancedActive => SelectedCategory == SettingsCategory.Advanced;

    partial void OnSelectedCategoryChanged(SettingsCategory value)
    {
        OnPropertyChanged(nameof(IsGeneralActive));
        OnPropertyChanged(nameof(IsSafetyActive));
        OnPropertyChanged(nameof(IsPerformanceActive));
        OnPropertyChanged(nameof(IsStorageLogsActive));
        OnPropertyChanged(nameof(IsAdvancedActive));
    }

    [RelayCommand]
    public void SelectCategory(SettingsCategory category)
    {
        SelectedCategory = category;
    }

    [ObservableProperty]
    private AppSettings _settings = AppSettings.CreateDefault();

    [ObservableProperty]
    private string _statusMessage = "";

    /// <summary>Opens the first-run wizard; supplied by the application because a view-model must not create windows.</summary>
    public Func<Task>? RunSetupWizardHandler { get; set; }

    [RelayCommand]
    private async Task RunSetupWizardAsync()
    {
        if (RunSetupWizardHandler == null)
        {
            StatusMessage = "The setup wizard is not available.";
            return;
        }

        await RunSetupWizardHandler();

        // The wizard saved through the shared settings; refresh this editor's copy so a later Save can't revert them.
        await LoadSettingsAsync();
    }

    [ObservableProperty]
    private ObservableCollection<ConflictResolution> _availableConflictStrategies;

    [ObservableProperty]
    private ObservableCollection<string> _availableThemes;

    public SettingsViewModel(
        SettingsRepository settingsRepository,
        TidyUp.Data.Recovery.IDatabaseBackupManager? backupManager = null,
        TidyUp.Data.Recovery.IDbIntegrityChecker? integrityChecker = null,
        TidyUp.Configuration.TidyUpPaths? paths = null)
    {
        _settingsRepository = settingsRepository;
        _backupManager = backupManager;
        _integrityChecker = integrityChecker;
        _paths = paths;

        // Initialize dropdowns
        _availableConflictStrategies = new ObservableCollection<ConflictResolution>(
            Enum.GetValues<ConflictResolution>()
        );

        _availableThemes = new ObservableCollection<string>(AppThemes.All);

        // Load settings asynchronously; UI starts with defaults until loaded
        _ = LoadSettingsAsync();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            Settings = await _settingsRepository.LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
            Settings = AppSettings.CreateDefault();
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            StatusMessage = "Saving settings...";
            await _settingsRepository.SaveAsync(Settings);
            StatusMessage = "Settings saved successfully";

            // Clear status after 3 seconds
            await Task.Delay(3000);
            StatusMessage = "";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        try
        {
            StatusMessage = "Resetting to defaults...";
            await _settingsRepository.ResetToDefaultsAsync();

            // Reload settings
            await LoadSettingsAsync();

            StatusMessage = "Settings reset to defaults";

            // Clear status after 3 seconds
            await Task.Delay(3000);
            StatusMessage = "";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resetting settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        try
        {
            if (_backupManager != null)
            {
                StatusMessage = "Creating database backup...";
                var path = await _backupManager.CreateBackupAsync(tag: "manual");
                StatusMessage = path != null
                    ? $"Database backup created: {System.IO.Path.GetFileName(path)}"
                    : "No database found to back up.";
            }
            else
            {
                StatusMessage = "Backup manager not available.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error backing up database: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task VacuumDatabaseAsync()
    {
        try
        {
            StatusMessage = "Optimizing SQLite database storage (VACUUM)...";

            var dbPath = _paths?.DatabasePath ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tidyup.db");
            if (_integrityChecker != null && System.IO.File.Exists(dbPath))
            {
                var result = await _integrityChecker.VacuumDatabaseAsync(dbPath);
                if (result.Success)
                {
                    if (result.ReclaimedBytes > 0)
                    {
                        var kb = result.ReclaimedBytes / 1024.0;
                        StatusMessage = $"Database optimized successfully. Reclaimed {kb:F1} KB ({result.ReclaimedBytes:N0} bytes) in {result.Duration.TotalMilliseconds:F0} ms.";
                    }
                    else
                    {
                        StatusMessage = $"Database already compact (0 bytes reclaimed). Optimization completed in {result.Duration.TotalMilliseconds:F0} ms.";
                    }
                }
                else
                {
                    StatusMessage = $"Error optimizing database: {result.ErrorMessage}";
                }
            }
            else
            {
                StatusMessage = "Database optimization service not available or database file not found.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error optimizing database: {ex.Message}";
        }
    }

    // ----- About (BRAND-03) -----

    /// <summary>"1.2.0 (fb8b11f)" for the About card.</summary>
    public string VersionText => AppVersionInfo.Display;

    public string CommitText => AppVersionInfo.Commit;

    public string RuntimeText => $".NET {Environment.Version.ToString(3)} on {Environment.OSVersion.VersionString}";

    [RelayCommand]
    private void OpenReleases() => OpenExternal("https://github.com/dazzo31/Tidy-up/releases");

    [RelayCommand]
    private void OpenLicense() => OpenExternal("https://github.com/dazzo31/Tidy-up/blob/main/LICENSE");

    [RelayCommand]
    private void OpenRepository() => OpenExternal("https://github.com/dazzo31/Tidy-up");

    /// <summary>
    /// Copies a support block (version, runtime, database path and schema) so a bug report does not need a back-and-forth.
    /// </summary>
    [RelayCommand]
    private void CopyDiagnostics()
    {
        try
        {
            System.Windows.Clipboard.SetText(BuildDiagnostics());
            StatusMessage = "Diagnostics copied to the clipboard.";
        }
        catch (Exception ex)
        {
            // The clipboard is a shared OS resource and can be locked by another process.
            StatusMessage = $"Could not copy diagnostics: {ex.Message}";
        }
    }

    /// <summary>Support block copied by <see cref="CopyDiagnostics"/>; also asserted by tests.</summary>
    public string BuildDiagnostics()
    {
        var paths = TidyUpPaths.FromEnvironmentOrDefault();
        var lines = new[]
        {
            $"TidyUp {AppVersionInfo.Display}",
            $"Runtime: {RuntimeText}",
            $"Data folder: {paths.AppDataRoot}",
            $"Database: {paths.DatabasePath}",
            $"Settings: {paths.SettingsPath}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private void OpenExternal(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open link: {ex.Message}";
        }
    }
}
