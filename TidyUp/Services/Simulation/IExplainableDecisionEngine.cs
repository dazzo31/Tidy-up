using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.Services.Simulation;

/// <summary>
/// Result item for a single condition evaluation on a file.
/// </summary>
public class ConditionExplanationItem
{
    public string ConditionType { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string ExpectedCriteria { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string DetailMessage { get; set; } = string.Empty;
}

/// <summary>
/// Complete explainability breakdown detailing why a file matched or did not match a rule.
/// </summary>
public class EvaluationBreakdown
{
    public string RuleName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool OverallMatch { get; set; }
    public string MatchSummary { get; set; } = string.Empty;
    public List<ConditionExplanationItem> ConditionResults { get; set; } = new();
}

/// <summary>
/// Service interface for computing human-understandable condition evaluation explanations.
/// </summary>
public interface IExplainableDecisionEngine
{
    EvaluationBreakdown ExplainMatch(Rule rule, string filePath);
}

/// <summary>
/// Default implementation of IExplainableDecisionEngine.
/// </summary>
public class ExplainableDecisionEngine : IExplainableDecisionEngine
{
    public EvaluationBreakdown ExplainMatch(Rule rule, string filePath)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(filePath);

        var fileInfo = new FileInfo(filePath);
        var breakdown = new EvaluationBreakdown
        {
            RuleName = rule.Name,
            FilePath = filePath
        };

        if (rule.Conditions == null || !rule.Conditions.Conditions.Any())
        {
            breakdown.OverallMatch = true;
            breakdown.MatchSummary = "Rule has no conditions: automatically matches all files.";
            return breakdown;
        }

        EvaluateGroup(rule.Conditions, fileInfo, breakdown.ConditionResults);

        breakdown.OverallMatch = rule.Conditions.Evaluate(fileInfo);
        int passedCount = breakdown.ConditionResults.Count(c => c.Passed);
        int totalCount = breakdown.ConditionResults.Count;

        breakdown.MatchSummary = breakdown.OverallMatch
            ? $"Matched: {passedCount} of {totalCount} condition checks succeeded under rule criteria."
            : $"Did not match: {totalCount - passedCount} condition check(s) failed.";

        return breakdown;
    }

    private void EvaluateGroup(ConditionGroup group, FileInfo fileInfo, List<ConditionExplanationItem> results)
    {
        foreach (var condition in group.Conditions)
        {
            if (condition is ConditionGroup subGroup)
            {
                EvaluateGroup(subGroup, fileInfo, results);
            }
            else
            {
                results.Add(ExplainSingleCondition(condition, fileInfo));
            }
        }
    }

    private ConditionExplanationItem ExplainSingleCondition(Condition condition, FileInfo fileInfo)
    {
        var item = new ConditionExplanationItem();

        switch (condition)
        {
            case FileNameCondition fn:
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileInfo.Name);
                item.ConditionType = "File Name";
                item.Operator = fn.Operator.ToString();
                item.ExpectedCriteria = fn.Value;
                item.ActualValue = nameWithoutExt;
                item.Passed = fn.Evaluate(fileInfo);
                item.DetailMessage = item.Passed
                    ? $"File name '{nameWithoutExt}' satisfies {fn.Operator} '{fn.Value}'"
                    : $"File name '{nameWithoutExt}' does not satisfy {fn.Operator} '{fn.Value}'";
                break;

            case FileExtensionCondition ext:
                var actualExt = fileInfo.Extension.TrimStart('.');
                item.ConditionType = "Extension";
                item.Operator = ext.Operator.ToString();
                item.ExpectedCriteria = ext.Value.TrimStart('.');
                item.ActualValue = actualExt;
                item.Passed = ext.Evaluate(fileInfo);
                item.DetailMessage = item.Passed
                    ? $"Extension '.{actualExt}' matches required '{ext.Value}'"
                    : $"Extension '.{actualExt}' does not match required '{ext.Value}'";
                break;

            case FileSizeCondition size:
                long bytes = fileInfo.Exists ? fileInfo.Length : 0;
                item.ConditionType = "File Size";
                item.Operator = size.Operator.ToString();
                item.ExpectedCriteria = $"{size.DisplaySize} {size.SizeUnit} ({size.Value:N0} bytes)";
                item.ActualValue = $"{bytes:N0} bytes";
                item.Passed = size.Evaluate(fileInfo);
                item.DetailMessage = item.Passed
                    ? $"File size of {bytes:N0} bytes is {size.Operator} {size.Value:N0} bytes"
                    : $"File size of {bytes:N0} bytes fails {size.Operator} {size.Value:N0} bytes";
                break;

            case FileDateCondition date:
                var actualDate = date.Type == FileDateCondition.DateType.Created
                    ? (fileInfo.Exists ? fileInfo.CreationTime : DateTime.MinValue)
                    : (fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue);
                item.ConditionType = $"Date ({date.Type})";
                item.Operator = date.Operator.ToString();
                item.ExpectedCriteria = date.Operator == FileDateCondition.DateOperator.OlderThanDays
                    ? $"{date.DaysOld} days old"
                    : date.Value.ToShortDateString();
                item.ActualValue = actualDate.ToShortDateString();
                item.Passed = date.Evaluate(fileInfo);
                item.DetailMessage = item.Passed
                    ? $"{date.Type} date {actualDate:d} matches criteria {date.Operator}"
                    : $"{date.Type} date {actualDate:d} does not match {date.Operator}";
                break;

            default:
                item.ConditionType = condition.GetType().Name;
                item.Operator = "Evaluate";
                item.ActualValue = fileInfo.Name;
                item.Passed = condition.Evaluate(fileInfo);
                item.DetailMessage = item.Passed ? "Condition evaluated to true" : "Condition evaluated to false";
                break;
        }

        return item;
    }
}
