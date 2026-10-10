using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.Services.Scheduling;

public enum ExecutionPauseReason
{
    None,
    RuleDisabled,
    ManualOnly,
    QuietHours,
    LowBattery,
    ScheduledNotDue
}

public class RuleExecutionEligibility
{
    public bool CanExecute { get; set; }
    public ExecutionPauseReason Reason { get; set; } = ExecutionPauseReason.None;
    public string Description { get; set; } = string.Empty;

    public static RuleExecutionEligibility Allowed() =>
        new() { CanExecute = true, Reason = ExecutionPauseReason.None, Description = "Ready to execute" };

    public static RuleExecutionEligibility Paused(ExecutionPauseReason reason, string description) =>
        new() { CanExecute = false, Reason = reason, Description = description };
}

public class QuietHoursConfig
{
    public bool IsEnabled { get; set; } = false;
    public TimeSpan StartTime { get; set; } = new(9, 0, 0); // 09:00 default
    public TimeSpan EndTime { get; set; } = new(17, 0, 0);  // 17:00 default
    public bool IncludeWeekends { get; set; } = true;

    public bool IsInQuietHours(DateTime now)
    {
        if (!IsEnabled)
            return false;

        if (!IncludeWeekends && (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday))
            return false;

        var timeOfDay = now.TimeOfDay;

        if (StartTime <= EndTime)
        {
            // Same day window, e.g. 09:00 to 17:00
            return timeOfDay >= StartTime && timeOfDay < EndTime;
        }
        else
        {
            // Spanning midnight, e.g. 22:00 to 06:00
            return timeOfDay >= StartTime || timeOfDay < EndTime;
        }
    }

    public TimeSpan? GetTimeUntilQuietHoursEnd(DateTime now)
    {
        if (!IsInQuietHours(now))
            return null;

        var targetEnd = now.Date.Add(EndTime);
        if (targetEnd <= now)
        {
            targetEnd = targetEnd.AddDays(1);
        }

        return targetEnd - now;
    }
}

public interface IScheduleManager
{
    QuietHoursConfig QuietHours { get; set; }
    bool PauseOnLowBattery { get; set; }
    double LowBatteryThresholdPercent { get; set; }

    bool IsInQuietHours(DateTime? evaluationTime = null);
    bool IsPowerConstrained();
    RuleExecutionEligibility CanExecuteRule(Rule rule, DateTime? evaluationTime = null, bool isManualTrigger = false);
    TimeSpan? GetTimeUntilQuietHoursEnd(DateTime? evaluationTime = null);
}

