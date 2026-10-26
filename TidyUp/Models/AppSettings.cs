using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using TidyUp.Models.Enums;

namespace TidyUp.Models;

/// <summary>
/// Application-wide settings and preferences
/// </summary>
public partial class AppSettings : ObservableObject
{
    /// <summary>Bumped when a persisted value needs migrating. 2: Theme is now actually applied.</summary>
    public const int CurrentSchemaVersion = 2;

    // Absent from files written before versioning, which therefore load as 0.
    [ObservableProperty]
    private int _schemaVersion;

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

    // "System", "Light" or "Dark". Kept as text so an unrecognised value in settings.json can never fail deserialization.
    [ObservableProperty]
    private string _theme = "System";

    // Safety Settings
    [ObservableProperty]
    private bool _useRecycleBinByDefault = true;

    [ObservableProperty]
    private bool _requireDestructiveConfirmations = true;

    // Performance Settings
    [ObservableProperty]
    private int _maxConcurrentOperations = 1;

    [ObservableProperty]
    private int _scanBatchSize = 500;

    // Advanced & Storage Settings
    [ObservableProperty]
    private bool _debugLoggingEnabled = false;

    [ObservableProperty]
    private string _databaseBackupPath = string.Empty;

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
        return new AppSettings { SchemaVersion = CurrentSchemaVersion };
    }

    /// <summary>Copies every setting from <paramref name="other"/>, raising change notifications.</summary>
    public void CopyFrom(AppSettings other)
    {
        foreach (var property in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(this, property.GetValue(other));
            }
        }
    }
}
