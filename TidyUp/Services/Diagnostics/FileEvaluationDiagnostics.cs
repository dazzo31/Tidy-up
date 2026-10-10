using TidyUp.Models.Enums;

namespace TidyUp.Services.Diagnostics;

/// <summary>
/// Comprehensive evaluation report explaining why a file matched or was rejected by a rule.
/// </summary>
public class FileEvaluationDiagnostics
{
    /// <summary>
    /// Identifier of the rule being evaluated.
    /// </summary>
    public Guid RuleId { get; init; }

    /// <summary>
    /// Name of the rule being evaluated.
    /// </summary>
    public string RuleName { get; init; } = string.Empty;

    /// <summary>
    /// Absolute path of the evaluated file.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// File name including extension.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Whether the rule is enabled. If false, overall evaluation is false regardless of conditions.
    /// </summary>
    public bool IsRuleEnabled { get; init; } = true;

    /// <summary>
    /// Whether the file satisfies all requirements and triggers the rule's actions.
    /// </summary>
    public bool IsOverallMatch { get; init; }

    /// <summary>
    /// High-level executive summary of the evaluation decision.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Root logic operator combining conditions (And / Or), if conditions exist.
    /// </summary>
    public LogicOperator? RootOperator { get; init; }

    /// <summary>
    /// Detailed diagnostic results for each evaluated condition or nested group.
    /// </summary>
    public List<ConditionEvaluationResult> Conditions { get; init; } = new();

    /// <summary>
    /// UTC timestamp when evaluation occurred.
    /// </summary>
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}

