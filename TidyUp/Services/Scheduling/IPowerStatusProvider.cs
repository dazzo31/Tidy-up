namespace TidyUp.Services.Scheduling;

/// <summary>
/// Provides device power and battery state information for execution throttling.
/// </summary>
public interface IPowerStatusProvider
{
    /// <summary>
    /// Returns true if the system is currently running on battery power (AC offline).
    /// </summary>
    bool IsOnBattery();

    /// <summary>
    /// Returns true if the system is running on battery and charge is at or below the threshold.
    /// </summary>
    bool IsLowBattery(double thresholdPercent = 20.0);

    /// <summary>
    /// Returns current battery charge percentage (0 to 100), or -1 if unavailable.
    /// </summary>
    double GetBatteryPercentage();
}

