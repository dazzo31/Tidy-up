using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Services.Scheduling;

namespace TidyUp.ViewModels;

public partial class ScheduleSettingsViewModel : ObservableObject
{
    private readonly IScheduleManager _scheduleManager;

    [ObservableProperty]
    private bool _quietHoursEnabled;

    [ObservableProperty]
    private int _startHour = 9;

    [ObservableProperty]
    private int _startMinute = 0;

    [ObservableProperty]
    private int _endHour = 17;

    [ObservableProperty]
    private int _endMinute = 0;

    [ObservableProperty]
    private bool _includeWeekends = true;

    [ObservableProperty]
    private bool _pauseOnLowBattery = true;

    [ObservableProperty]
    private double _lowBatteryThresholdPercent = 20.0;

    [ObservableProperty]
    private string _currentStatusText = string.Empty;

    public ScheduleSettingsViewModel(IScheduleManager scheduleManager)
    {
        _scheduleManager = scheduleManager;
        LoadFromManager();
        UpdateStatus();
    }

    private void LoadFromManager()
    {
        QuietHoursEnabled = _scheduleManager.QuietHours.IsEnabled;
        StartHour = _scheduleManager.QuietHours.StartTime.Hours;
        StartMinute = _scheduleManager.QuietHours.StartTime.Minutes;
        EndHour = _scheduleManager.QuietHours.EndTime.Hours;
        EndMinute = _scheduleManager.QuietHours.EndTime.Minutes;
        IncludeWeekends = _scheduleManager.QuietHours.IncludeWeekends;
        PauseOnLowBattery = _scheduleManager.PauseOnLowBattery;
        LowBatteryThresholdPercent = _scheduleManager.LowBatteryThresholdPercent;
    }

    [RelayCommand]
    public void Save()
    {
        _scheduleManager.QuietHours.IsEnabled = QuietHoursEnabled;
        _scheduleManager.QuietHours.StartTime = new TimeSpan(StartHour, StartMinute, 0);
        _scheduleManager.QuietHours.EndTime = new TimeSpan(EndHour, EndMinute, 0);
        _scheduleManager.QuietHours.IncludeWeekends = IncludeWeekends;
        _scheduleManager.PauseOnLowBattery = PauseOnLowBattery;
        _scheduleManager.LowBatteryThresholdPercent = LowBatteryThresholdPercent;

        UpdateStatus();
    }

    [RelayCommand]
    public void RefreshStatus()
    {
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_scheduleManager.IsInQuietHours())
        {
            CurrentStatusText = $"Active - In Quiet Hours until {_scheduleManager.QuietHours.EndTime:hh\\:mm}";
        }
        else if (_scheduleManager.IsPowerConstrained())
        {
            CurrentStatusText = "Active - Paused due to Low Battery";
        }
        else
        {
            CurrentStatusText = "Normal - Automatic background processing active";
        }
    }
}

