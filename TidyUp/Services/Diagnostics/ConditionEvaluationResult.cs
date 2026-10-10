namespace TidyUp.Services.Diagnostics;

/// <summary>
/// Detailed evaluation diagnostic result for an individual rule condition or nested condition group.
/// </summary>
public class ConditionEvaluationResult
{
    /// <summary>
    /// Unique identifier of the condition that was evaluated.
    /// </summary>
    public Guid ConditionId { get; init; }

    /// <summary>
    /// Type of condition (e.g. "FileName", "FileExtension", "FileSize", "FileDate", "ConditionGroup").
    /// </summary>
    public string ConditionType { get; init; } = string.Empty;

    /// <summary>
    /// Comparison operator applied (e.g. "Is", "Contains", "GreaterThan", "Between", "OlderThanDays", "And", "Or").
    /// </summary>
    public string Operator { get; init; } = string.Empty;

    /// <summary>
    /// Target or expected value(s) specified in the condition definition.
    /// </summary>
    public string TargetValue { get; init; } = string.Empty;

    /// <summary>
    /// Actual file property value extracted during evaluation.
    /// </summary>
    public string ActualValue { get; init; } = string.Empty;

    /// <summary>
    /// Whether this condition was satisfied by the file.
    /// </summary>
    public bool IsMatch { get; init; }

    /// <summary>
    /// Clear human-readable diagnostic explanation detailing why the condition passed or failed.
    /// </summary>
    public string Explanation { get; init; } = string.Empty;

    /// <summary>
    /// Evaluation results for child conditions if this result represents a ConditionGroup.
    /// </summary>
    public List<ConditionEvaluationResult> Children { get; init; } = new();
}

