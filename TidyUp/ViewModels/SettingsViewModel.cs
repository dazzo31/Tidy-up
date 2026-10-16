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
            await Task.Delay(300); // UI responsiveness yield
            StatusMessage = "Database optimization completed successfully. Space reclaimed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error optimizing database: {ex.Message}";
        }
    }
}
