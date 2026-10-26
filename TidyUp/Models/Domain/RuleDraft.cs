using System.Text.Json;
using System.Text.Json.Serialization;

namespace TidyUp.Models.Domain;

/// <summary>
/// Encapsulates an isolated deep-cloned rule draft for non-destructive editing across
/// Guided and Advanced editors. Prevents memory corruption, dirty-tracking leaks, and
/// overwriting runtime execution statistics.
/// </summary>
public class RuleDraft
{
    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// The active mutable working copy edited by UI viewmodels.
    /// </summary>
    public Rule WorkingCopy { get; private set; }

    /// <summary>
    /// Immutable baseline snapshot representing the last persisted database state.
    /// </summary>
    public Rule BaselineSnapshot { get; private set; }

    /// <summary>
    /// Initializes a new draft session from an existing rule.
    /// </summary>
    public RuleDraft(Rule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        BaselineSnapshot = DeepCloneRule(rule);
        WorkingCopy = DeepCloneRule(rule);
    }

    /// <summary>
    /// Evaluates whether the working copy has uncommitted changes compared to the baseline snapshot.
    /// Compares JSON serialized representations to capture nested condition and action edits accurately.
    /// </summary>
    public bool IsDirty
    {
        get
        {
            var baselineJson = SerializeForComparison(BaselineSnapshot);
            var workingJson = SerializeForComparison(WorkingCopy);
            return !string.Equals(baselineJson, workingJson, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Discards all changes in the working copy and restores the baseline snapshot.
    /// </summary>
    public void ResetToBaseline()
    {
        WorkingCopy = DeepCloneRule(BaselineSnapshot);
    }

    /// <summary>
    /// Updates the baseline snapshot after a successful database save.
    /// </summary>
    public void CommitBaseline(Rule updatedSavedRule)
    {
        ArgumentNullException.ThrowIfNull(updatedSavedRule);
        BaselineSnapshot = DeepCloneRule(updatedSavedRule);
        WorkingCopy = DeepCloneRule(updatedSavedRule);
    }

    /// <summary>
    /// Creates a complete, independent deep clone of a Rule entity.
    /// </summary>
    public static Rule DeepCloneRule(Rule source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var json = JsonSerializer.Serialize(source, CloneOptions);
        var clone = JsonSerializer.Deserialize<Rule>(json, CloneOptions);
        return clone ?? throw new InvalidOperationException("Failed to deserialize cloned rule.");
    }

    private static string SerializeForComparison(Rule rule)
    {
        // Exclude runtime execution statistics and transient UI properties from dirty comparison
        return JsonSerializer.Serialize(new
        {
            rule.Id,
            rule.Name,
            rule.Description,
            rule.IsEnabled,
            rule.ExecutionOrder,
            rule.StopProcessingAfterMatch,
            rule.TriggerType,
            rule.ScheduledTime,
            rule.MonitoredFolders,
            rule.Conditions,
            rule.Actions
        }, CloneOptions);
    }
}
