namespace TidyUp.Models.Enums;

/// <summary>
/// Specifies the execution trigger mode for an organization rule.
/// </summary>
public enum RuleTriggerType
{
    /// <summary>
    /// Evaluates and runs automatically whenever file changes are detected by the watcher.
    /// </summary>
    Continuous,

    /// <summary>
    /// Executes only at specified scheduled times or recurring intervals.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Never runs automatically; only executes when manually triggered by the user.
    /// </summary>
    ManualOnly
}

