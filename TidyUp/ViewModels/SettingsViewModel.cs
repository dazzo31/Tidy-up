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

    [ObservableProperty]
    private AppSettings _settings;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private ObservableCollection<ConflictResolution> _availableConflictStrategies;

    [ObservableProperty]
    private ObservableCollection<string> _availableThemes;

    public SettingsViewModel(SettingsRepository settingsRepository, AppSettings settings)
    {
        _settingsRepository = settingsRepository;
        _settings = settings;

        // Initialize dropdowns
        _availableConflictStrategies = new ObservableCollection<ConflictResolution>(
            Enum.GetValues<ConflictResolution>()
        );

        _availableThemes = new ObservableCollection<string>
        {
            "Dark",
            "Light"
        };
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
            Settings = await _settingsRepository.LoadAsync();
            
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
}
