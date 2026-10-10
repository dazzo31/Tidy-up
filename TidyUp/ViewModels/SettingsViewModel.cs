using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Data;
using TidyUp.Models;
using TidyUp.Models.Enums;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for application settings.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsRepository _settingsRepository;
    private readonly TidyUp.Data.Recovery.IDatabaseBackupManager? _backupManager;

    [ObservableProperty]
    private AppSettings _settings = AppSettings.CreateDefault();

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private ObservableCollection<ConflictResolution> _availableConflictStrategies;

    [ObservableProperty]
    private ObservableCollection<string> _availableThemes;

    public SettingsViewModel(SettingsRepository settingsRepository, TidyUp.Data.Recovery.IDatabaseBackupManager? backupManager = null)
    {
        _settingsRepository = settingsRepository;
        _backupManager = backupManager;

        // Initialize dropdowns
        _availableConflictStrategies = new ObservableCollection<ConflictResolution>(
            Enum.GetValues<ConflictResolution>()
        );

        _availableThemes =
        [
            "Dark",
            "Light"
        ];

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
}
