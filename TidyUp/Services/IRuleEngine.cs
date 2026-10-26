using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Services.Diagnostics;

namespace TidyUp.Services;

/// <summary>
/// Specifies the evaluation purpose for rule matching.
/// </summary>
public enum RuleEvaluationPurpose
{
    /// <summary>
    /// Live execution by watcher or coordinator. Enforces rule.IsEnabled == true.
    /// </summary>
    LiveExecution,

    /// <summary>
    /// Read-only dry-run simulation, preview, or testing. Evaluates conditions regardless of rule.IsEnabled.
    /// </summary>
    SimulationOrPreview
}

/// <summary>
/// Service for evaluating rules against files.
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Evaluates whether a file matches the given rule's conditions for live execution (enforces rule.IsEnabled).
    /// </summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="fileInfo">The file to check.</param>
    /// <returns>True if the file matches the rule's conditions and rule is enabled.</returns>
    bool EvaluateRule(Rule rule, FileInfo fileInfo);

    /// <summary>
    /// Evaluates whether a file matches the given rule's conditions for the specified purpose.
    /// </summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="fileInfo">The file to check.</param>
    /// <param name="purpose">Evaluation purpose (LiveExecution enforces IsEnabled; SimulationOrPreview evaluates conditions only).</param>
    /// <returns>True if the file matches.</returns>
    bool EvaluateRule(Rule rule, FileInfo fileInfo, RuleEvaluationPurpose purpose);

    /// <summary>
    /// Evaluates whether a file matches the rule's conditions for dry-run simulation/preview without checking or modifying live execution eligibility.
    /// </summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="fileInfo">The file to check.</param>
    /// <returns>True if file matches rule conditions.</returns>
    bool MatchesConditions(Rule rule, FileInfo fileInfo);

    /// <summary>
    /// Gets all rules that match the given file, in execution order.
    /// </summary>
    /// <param name="rules">List of rules to check.</param>
    /// <param name="fileInfo">The file to match.</param>
    /// <param name="stopOnFirstMatch">If true, stop after first matching rule.</param>
    /// <returns>List of matching rules.</returns>
    List<Rule> GetMatchingRules(List<Rule> rules, FileInfo fileInfo, bool stopOnFirstMatch = false);

    /// <summary>
    /// Provides detailed condition-by-condition match diagnostics explaining why a file was matched or rejected by a rule.
    /// </summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="fileInfo">The file to check.</param>
    /// <param name="purpose">Evaluation purpose.</param>
    /// <returns>Comprehensive diagnostics explaining the evaluation outcome.</returns>
    FileEvaluationDiagnostics ExplainEvaluation(Rule rule, FileInfo fileInfo, RuleEvaluationPurpose purpose = RuleEvaluationPurpose.LiveExecution);

    /// <summary>
    /// Provides detailed condition-by-condition match diagnostics explaining why a file was matched or rejected by a rule.
    /// </summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="filePath">Absolute path of file to check.</param>
    /// <param name="purpose">Evaluation purpose.</param>
    /// <returns>Comprehensive diagnostics explaining the evaluation outcome.</returns>
    FileEvaluationDiagnostics ExplainEvaluation(Rule rule, string filePath, RuleEvaluationPurpose purpose = RuleEvaluationPurpose.LiveExecution);
}
