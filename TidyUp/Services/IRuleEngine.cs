using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Services.Diagnostics;

namespace TidyUp.Services;

/// <summary>
/// Service for evaluating rules against files.
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Evaluates whether a file matches the given rule's conditions.
    /// </summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="fileInfo">The file to check.</param>
    /// <returns>True if the file matches the rule's conditions.</returns>
    bool EvaluateRule(Rule rule, FileInfo fileInfo);

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
    /// <returns>Comprehensive diagnostics explaining the evaluation outcome.</returns>
    FileEvaluationDiagnostics ExplainEvaluation(Rule rule, FileInfo fileInfo);

    /// <summary>
    /// Provides detailed condition-by-condition match diagnostics explaining why a file was matched or rejected by a rule.
    /// </summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="filePath">Absolute path of file to check.</param>
    /// <returns>Comprehensive diagnostics explaining the evaluation outcome.</returns>
    FileEvaluationDiagnostics ExplainEvaluation(Rule rule, string filePath);
}
