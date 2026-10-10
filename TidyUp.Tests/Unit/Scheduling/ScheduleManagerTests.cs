using Moq;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Scheduling;
using Xunit;

namespace TidyUp.Tests.Unit.Scheduling;

public class ScheduleManagerTests
{
    private readonly Mock<IPowerStatusProvider> _mockPowerProvider;
    private readonly ScheduleManager _scheduleManager;

    public ScheduleManagerTests()
    {
        _mockPowerProvider = new Mock<IPowerStatusProvider>();
        // Default: AC power, healthy battery
        _mockPowerProvider.Setup(p => p.IsOnBattery()).Returns(false);
        _mockPowerProvider.Setup(p => p.IsLowBattery(It.IsAny<double>())).Returns(false);
        _mockPowerProvider.Setup(p => p.GetBatteryPercentage()).Returns(100.0);

        _scheduleManager = new ScheduleManager(_mockPowerProvider.Object);
    }

    [Fact]
    public void CanExecuteRule_WhenInQuietHours_DefersExecution()
    {
        // Arrange: Quiet hours 09:00 - 17:00
        _scheduleManager.QuietHours = new QuietHoursConfig
        {
            IsEnabled = true,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            IncludeWeekends = true
        };

        var rule = new Rule { Name = "Clean Downloads", IsEnabled = true, TriggerType = RuleTriggerType.Continuous };
        var testTime = new DateTime(2026, 10, 12, 14, 30, 0); // Monday at 14:30

        // Act
        var eligibility = _scheduleManager.CanExecuteRule(rule, testTime);

        // Assert
        Assert.False(eligibility.CanExecute);
        Assert.Equal(ExecutionPauseReason.QuietHours, eligibility.Reason);
        Assert.Contains("paused during quiet hours", eligibility.Description);
    }

    [Fact]
    public void CanExecuteRule_WhenQuietHoursSpanningMidnight_DefersExecution()
    {
        // Arrange: Quiet hours 22:00 - 06:00 (overnight)
        _scheduleManager.QuietHours = new QuietHoursConfig
        {
            IsEnabled = true,
            StartTime = new TimeSpan(22, 0, 0),
            EndTime = new TimeSpan(6, 0, 0),
            IncludeWeekends = true
        };

        var rule = new Rule { Name = "Night Rule", IsEnabled = true, TriggerType = RuleTriggerType.Continuous };

        // Test at 23:30 (before midnight)
        var lateNight = new DateTime(2026, 10, 12, 23, 30, 0);
        var lateNightResult = _scheduleManager.CanExecuteRule(rule, lateNight);
        Assert.False(lateNightResult.CanExecute);
        Assert.Equal(ExecutionPauseReason.QuietHours, lateNightResult.Reason);

        // Test at 03:15 (after midnight)
        var earlyMorning = new DateTime(2026, 10, 13, 3, 15, 0);
        var earlyMorningResult = _scheduleManager.CanExecuteRule(rule, earlyMorning);
        Assert.False(earlyMorningResult.CanExecute);
        Assert.Equal(ExecutionPauseReason.QuietHours, earlyMorningResult.Reason);

        // Test at 10:00 (outside quiet hours)
        var dayTime = new DateTime(2026, 10, 13, 10, 0, 0);
        var dayResult = _scheduleManager.CanExecuteRule(rule, dayTime);
        Assert.True(dayResult.CanExecute);
    }

    [Fact]
    public void CanExecuteRule_WhenQuietHoursDisabled_AllowsExecution()
    {
        // Arrange: Quiet hours disabled
        _scheduleManager.QuietHours = new QuietHoursConfig
        {
            IsEnabled = false,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(17, 0, 0)
        };

        var rule = new Rule { Name = "Active Rule", IsEnabled = true };
        var testTime = new DateTime(2026, 10, 12, 12, 0, 0);

        // Act
        var eligibility = _scheduleManager.CanExecuteRule(rule, testTime);

        // Assert
        Assert.True(eligibility.CanExecute);
        Assert.Equal(ExecutionPauseReason.None, eligibility.Reason);
    }

    [Fact]
    public void CanExecuteRule_WhenWeekendAndWeekendsExcluded_AllowsExecution()
    {
        // Arrange: Quiet hours active on weekdays only
        _scheduleManager.QuietHours = new QuietHoursConfig
        {
            IsEnabled = true,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            IncludeWeekends = false
        };

        var rule = new Rule { Name = "Weekend Rule", IsEnabled = true };
        var sundayAfternoon = new DateTime(2026, 10, 11, 14, 0, 0); // Sunday

        // Act
        var eligibility = _scheduleManager.CanExecuteRule(rule, sundayAfternoon);

        // Assert
        Assert.True(eligibility.CanExecute);
    }

    [Fact]
    public void CanExecuteRule_WhenLowBattery_DefersExecution()
    {
        // Arrange: Quiet hours disabled, but battery is at 15% (below 20% threshold)
        _scheduleManager.QuietHours.IsEnabled = false;
        _scheduleManager.PauseOnLowBattery = true;
        _scheduleManager.LowBatteryThresholdPercent = 20.0;

        _mockPowerProvider.Setup(p => p.IsLowBattery(20.0)).Returns(true);
        _mockPowerProvider.Setup(p => p.GetBatteryPercentage()).Returns(15.0);

        var rule = new Rule { Name = "Battery Sensitive", IsEnabled = true };

        // Act
        var eligibility = _scheduleManager.CanExecuteRule(rule);

        // Assert
        Assert.False(eligibility.CanExecute);
        Assert.Equal(ExecutionPauseReason.LowBattery, eligibility.Reason);
        Assert.Contains("low battery", eligibility.Description);
    }

    [Fact]
    public void CanExecuteRule_WhenManualTrigger_BypassesQuietHoursAndBattery()
    {
        // Arrange: Both quiet hours and low battery active
        _scheduleManager.QuietHours = new QuietHoursConfig
        {
            IsEnabled = true,
            StartTime = new TimeSpan(0, 0, 0),
            EndTime = new TimeSpan(23, 59, 59)
        };
        _scheduleManager.PauseOnLowBattery = true;
        _mockPowerProvider.Setup(p => p.IsLowBattery(It.IsAny<double>())).Returns(true);

        var rule = new Rule { Name = "Manual Triggered Rule", IsEnabled = true };

        // Act: isManualTrigger = true
        var eligibility = _scheduleManager.CanExecuteRule(rule, isManualTrigger: true);

        // Assert
        Assert.True(eligibility.CanExecute);
        Assert.Equal(ExecutionPauseReason.None, eligibility.Reason);
    }

    [Fact]
    public void CanExecuteRule_WhenRuleDisabled_DefersExecutionEvenIfManuallyTriggered()
    {
        // Arrange
        var rule = new Rule { Name = "Disabled Rule", IsEnabled = false };

        // Act
        var eligibility = _scheduleManager.CanExecuteRule(rule, isManualTrigger: false);

        // Assert
        Assert.False(eligibility.CanExecute);
        Assert.Equal(ExecutionPauseReason.RuleDisabled, eligibility.Reason);
    }

    [Fact]
    public void CanExecuteRule_ScheduledRule_DefersExecutionBeforeDueTime()
    {
        // Arrange
        _scheduleManager.QuietHours.IsEnabled = false;
        _scheduleManager.PauseOnLowBattery = false;

        var rule = new Rule
        {
            Name = "Daily 18:00 Run",
            IsEnabled = true,
            TriggerType = RuleTriggerType.Scheduled,
            ScheduledTime = new TimeSpan(18, 0, 0)
        };

        var morningTime = new DateTime(2026, 10, 12, 10, 0, 0); // 10:00 (before 18:00)

        // Act
        var eligibility = _scheduleManager.CanExecuteRule(rule, morningTime);

        // Assert
        Assert.False(eligibility.CanExecute);
        Assert.Equal(ExecutionPauseReason.ScheduledNotDue, eligibility.Reason);
    }

    [Fact]
    public void CanExecuteRule_ScheduledRule_AllowsExecutionAtOrAfterDueTime()
    {
        // Arrange
        _scheduleManager.QuietHours.IsEnabled = false;
        _scheduleManager.PauseOnLowBattery = false;

        var rule = new Rule
        {
            Name = "Daily 18:00 Run",
            IsEnabled = true,
            TriggerType = RuleTriggerType.Scheduled,
            ScheduledTime = new TimeSpan(18, 0, 0),
            LastRunDate = new DateTime(2026, 10, 11, 18, 5, 0) // Ran yesterday
        };

        var eveningTime = new DateTime(2026, 10, 12, 18, 30, 0); // 18:30 today

        // Act
        var eligibility = _scheduleManager.CanExecuteRule(rule, eveningTime);

        // Assert
        Assert.True(eligibility.CanExecute);
    }
}

