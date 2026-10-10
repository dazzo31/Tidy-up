using TidyUp.Models.Domain;

namespace TidyUp.Services.Rules;

/// <summary>
/// Service that translates rule configurations (folders, condition trees, and action pipelines)
/// into plain-language human-readable sentences.
/// </summary>
public interface IRuleSummaryGenerator
{
    /// <summary>
    /// Generates a complete natural-language sentence summarizing the full rule behavior.
    /// </summary>
    string GenerateSummary(Rule rule);

    /// <summary>
    /// Generates a plain-language summary describing condition criteria.
    /// </summary>
    string GenerateConditionSummary(ConditionGroup? conditions);

    /// <summary>
    /// Generates a plain-language summary describing a single file action.
    /// </summary>
    string GenerateActionSummary(FileAction action);
}

