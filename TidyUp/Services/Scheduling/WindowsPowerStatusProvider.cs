using System.Runtime.InteropServices;

namespace TidyUp.Services.Scheduling;

/// <summary>
/// Windows implementation of IPowerStatusProvider querying Win32 GetSystemPowerStatus.
/// </summary>
public class WindowsPowerStatusProvider : IPowerStatusProvider
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;          // 0: Offline, 1: Online, 255: Unknown
        public byte BatteryFlag;           // 1: High, 2: Low, 4: Critical, 8: Charging, 128: No system battery, 255: Unknown
        public byte BatteryLifePercent;    // 0..100, 255: Unknown
        public byte SystemStatusFlag;      // 1: Battery saver active
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus sps);

    public bool IsOnBattery()
    {
        if (GetSystemPowerStatus(out var status))
        {
            // ACLineStatus == 0 means unplugged / on battery
            return status.ACLineStatus == 0;
        }

        return false;
    }

    public bool IsLowBattery(double thresholdPercent = 20.0)
    {
        if (GetSystemPowerStatus(out var status))
        {
            if (status.ACLineStatus != 0)
                return false; // On AC power, not running down battery

            if (status.BatteryLifePercent != 255)
            {
                return status.BatteryLifePercent <= thresholdPercent;
            }

            // Fallback: BatteryFlag has bit 2 (Low) or 4 (Critical) set
            return (status.BatteryFlag & 2) != 0 || (status.BatteryFlag & 4) != 0;
        }

        return false;
    }

    public double GetBatteryPercentage()
    {
        if (GetSystemPowerStatus(out var status) && status.BatteryLifePercent != 255)
        {
            return status.BatteryLifePercent;
        }

        return -1;
    }
}

