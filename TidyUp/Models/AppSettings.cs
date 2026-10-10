using CommunityToolkit.Mvvm.ComponentModel;
using TidyUp.Models.Enums;

namespace TidyUp.Models;

/// <summary>
/// Application-wide settings and preferences
/// </summary>
public partial class AppSettings : ObservableObject
{
    // General Settings
    [ObservableProperty]
    private ConflictResolution _defaultConflictResolution = ConflictResolution.RenameNew;

    // Monitoring Settings
    [ObservableProperty]
    private int _debounceDelayMs = 500;

    [ObservableProperty]
    private int _networkFolderPollingIntervalSeconds = 30;

    [ObservableProperty]
    private int _fileLockRetryCount = 3;

    [ObservableProperty]
    private int _fileLockRetryDelayMs = 1000;

    // Log Settings
    [ObservableProperty]
    private int _logRetentionDays = 30;

    [ObservableProperty]
    private bool _autoCleanupLogs = true;

    // Notification Settings
    [ObservableProperty]
    private bool _systemTrayEnabled = true;

    [ObservableProperty]
    private bool _toastNotificationsEnabled = true;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _showSuccessNotifications = false;

    [ObservableProperty]
    private bool _showErrorNotifications = true;

    [ObservableProperty]
    private bool _quietMode;

    // UI Settings
    [ObservableProperty]
    private bool _showFirstRunWizard = true;

    [ObservableProperty]
    private string _theme = "Dark";

    // Window Geometry Persistence (TASK-GUI-08)
    [ObservableProperty]
    private double? _windowWidth;

    [ObservableProperty]
    private double? _windowHeight;

    [ObservableProperty]
    private double? _windowTop;

    [ObservableProperty]
    private double? _windowLeft;

    [ObservableProperty]
    private string _windowState = "Normal";

    /// <summary>
    /// Creates a new instance with default values
    /// </summary>
    public static AppSettings CreateDefault()
    {
        return new AppSettings();
    }
}
