using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.Services.Scheduling;

public class ScheduleManager : IScheduleManager
{
    private readonly IPowerStatusProvider _powerStatusProvider;

    public QuietHoursConfig QuietHours { get; set; } = new();
    public bool PauseOnLowBattery { get; set; } = true;
    public double LowBatteryThresholdPercent { get; set; } = 20.0;

    public ScheduleManager(IPowerStatusProvider powerStatusProvider)
    {
        _powerStatusProvider = powerStatusProvider;
    }

    public bool IsInQuietHours(DateTime? evaluationTime = null)
    {
        var time = evaluationTime ?? DateTime.Now;
        return QuietHours.IsInQuietHours(time);
    }

    public bool IsPowerConstrained()
    {
        if (!PauseOnLowBattery)
            return false;

        return _powerStatusProvider.IsLowBattery(LowBatteryThresholdPercent);
    }

    public TimeSpan? GetTimeUntilQuietHoursEnd(DateTime? evaluationTime = null)
    {
        var time = evaluationTime ?? DateTime.Now;
        return QuietHours.GetTimeUntilQuietHoursEnd(time);
    }

    public RuleExecutionEligibility CanExecuteRule(Rule rule, DateTime? evaluationTime = null, bool isManualTrigger = false)
    {
        var now = evaluationTime ?? DateTime.Now;

        // 1. Disabled rule never runs
        if (!rule.IsEnabled)
        {
            return RuleExecutionEligibility.Paused(ExecutionPauseReason.RuleDisabled, $"Rule '{rule.Name}' is disabled.");
        }

        // Manual triggers bypass quiet hours, scheduling, and power constraints
        if (isManualTrigger)
        {
            return RuleExecutionEligibility.Allowed();
        }

        // 2. ManualOnly rules do not run automatically
        if (rule.TriggerType == RuleTriggerType.ManualOnly)
        {
            return RuleExecutionEligibility.Paused(ExecutionPauseReason.ManualOnly, $"Rule '{rule.Name}' is configured for manual-only execution.");
        }

        // 3. Quiet Hours check
        if (QuietHours.IsInQuietHours(now))
        {
            var remaining = QuietHours.GetTimeUntilQuietHoursEnd(now);
            var remainingStr = remaining.HasValue ? $" for another {remaining.Value.Hours}h {remaining.Value.Minutes}m" : string.Empty;
            return RuleExecutionEligibility.Paused(
                ExecutionPauseReason.QuietHours,
                $"Background file organization is paused during quiet hours{remainingStr} ({QuietHours.StartTime:hh\\:mm} - {QuietHours.EndTime:hh\\:mm}).");
        }

        // 4. Windows Power / Battery check
        if (PauseOnLowBattery && _powerStatusProvider.IsLowBattery(LowBatteryThresholdPercent))
        {
            var batteryPct = _powerStatusProvider.GetBatteryPercentage();
            var pctStr = batteryPct >= 0 ? $" at {batteryPct:F0}%" : string.Empty;
            return RuleExecutionEligibility.Paused(
                ExecutionPauseReason.LowBattery,
                $"Background file organization is paused to preserve power while running on low battery{pctStr}.");
        }

        // 5. Scheduled Rule evaluation
        if (rule.TriggerType == RuleTriggerType.Scheduled && rule.ScheduledTime.HasValue)
        {
            var scheduledTime = rule.ScheduledTime.Value;
            var currentTimeOfDay = now.TimeOfDay;

            // If rule was already executed today after the scheduled time, it is not due yet
            if (rule.LastRunDate.HasValue && rule.LastRunDate.Value.Date == now.Date && rule.LastRunDate.Value.TimeOfDay >= scheduledTime)
            {
                return RuleExecutionEligibility.Paused(
                    ExecutionPauseReason.ScheduledNotDue,
                    $"Rule '{rule.Name}' was already executed today at {rule.LastRunDate.Value:t}; next run at {scheduledTime:hh\\:mm}.");
            }

            // If current time is before the scheduled time, it is not due yet
            if (currentTimeOfDay < scheduledTime)
            {
                return RuleExecutionEligibility.Paused(
                    ExecutionPauseReason.ScheduledNotDue,
                    $"Rule '{rule.Name}' is scheduled to run at {scheduledTime:hh\\:mm}.");
            }
        }

        return RuleExecutionEligibility.Allowed();
    }
}

