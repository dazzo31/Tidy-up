using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Diagnostics;

namespace TidyUp.Services;

/// <summary>
/// Implementation of rule evaluation engine with explainability and condition diagnosis.
/// </summary>
public class RuleEngine : IRuleEngine
{
    public bool EvaluateRule(Rule rule, FileInfo fileInfo)
    {
        if (rule == null || !rule.IsEnabled)
            return false;

        // If rule has no conditions, it matches all files
        if (rule.Conditions == null)
            return true;

        // Evaluate the condition tree
        return rule.Conditions.Evaluate(fileInfo);
    }

    public List<Rule> GetMatchingRules(List<Rule> rules, FileInfo fileInfo, bool stopOnFirstMatch = false)
    {
        var matchingRules = new List<Rule>();

        // Sort by execution order
        var sortedRules = rules
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.ExecutionOrder)
            .ToList();

        foreach (var rule in sortedRules)
        {
            if (EvaluateRule(rule, fileInfo))
            {
                matchingRules.Add(rule);

                // If this rule has StopProcessingAfterMatch, stop here
                if (stopOnFirstMatch || rule.StopProcessingAfterMatch)
                    break;
            }
        }

        return matchingRules;
    }

    public FileEvaluationDiagnostics ExplainEvaluation(Rule rule, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new FileEvaluationDiagnostics
            {
                RuleId = rule?.Id ?? Guid.Empty,
                RuleName = rule?.Name ?? string.Empty,
                FilePath = string.Empty,
                FileName = string.Empty,
                IsRuleEnabled = rule?.IsEnabled ?? false,
                IsOverallMatch = false,
                Summary = "Invalid or empty file path."
            };
        }

        var fileInfo = new FileInfo(filePath);
        return ExplainEvaluation(rule, fileInfo);
    }

    public FileEvaluationDiagnostics ExplainEvaluation(Rule rule, FileInfo fileInfo)
    {
        if (rule == null)
        {
            return new FileEvaluationDiagnostics
            {
                RuleId = Guid.Empty,
                RuleName = string.Empty,
                FilePath = fileInfo?.FullName ?? string.Empty,
                FileName = fileInfo?.Name ?? string.Empty,
                IsRuleEnabled = false,
                IsOverallMatch = false,
                Summary = "No rule provided for evaluation."
            };
        }

        var filePath = fileInfo?.FullName ?? string.Empty;
        var fileName = fileInfo?.Name ?? string.Empty;

        if (!rule.IsEnabled)
        {
            return new FileEvaluationDiagnostics
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                FilePath = filePath,
                FileName = fileName,
                IsRuleEnabled = false,
                IsOverallMatch = false,
                Summary = $"Rule '{rule.Name}' is disabled."
            };
        }

        if (rule.Conditions == null || !rule.Conditions.Conditions.Any())
        {
            return new FileEvaluationDiagnostics
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                FilePath = filePath,
                FileName = fileName,
                IsRuleEnabled = true,
                IsOverallMatch = true,
                Summary = $"Rule '{rule.Name}' has no conditions configured and matches all files by default.",
                RootOperator = rule.Conditions?.Operator
            };
        }

        var rootGroup = rule.Conditions;
        var groupResult = EvaluateConditionGroup(rootGroup, fileInfo!);

        var summary = groupResult.IsMatch
            ? $"File '{fileName}' matches rule '{rule.Name}' ({rootGroup.Operator} condition satisfied)."
            : $"File '{fileName}' was rejected by rule '{rule.Name}': {GetRejectionSummary(groupResult, rootGroup.Operator)}";

        return new FileEvaluationDiagnostics
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            FilePath = filePath,
            FileName = fileName,
            IsRuleEnabled = true,
            IsOverallMatch = groupResult.IsMatch,
            Summary = summary,
            RootOperator = rootGroup.Operator,
            Conditions = groupResult.Children
        };
    }

    private static ConditionEvaluationResult EvaluateCondition(Condition condition, FileInfo fileInfo)
    {
        return condition switch
        {
            ConditionGroup group => EvaluateConditionGroup(group, fileInfo),
            FileNameCondition nameCond => EvaluateFileName(nameCond, fileInfo),
            FileExtensionCondition extCond => EvaluateFileExtension(extCond, fileInfo),
            FileSizeCondition sizeCond => EvaluateFileSize(sizeCond, fileInfo),
            FileDateCondition dateCond => EvaluateFileDate(dateCond, fileInfo),
            FileContentCondition contentCond => new ConditionEvaluationResult
            {
                ConditionId = contentCond.Id,
                ConditionType = "FileContent",
                IsMatch = contentCond.Evaluate(fileInfo),
                Explanation = $"Content {(contentCond.Evaluate(fileInfo) ? "matched" : "did not match")} pattern '{contentCond.SearchText}'."
            },
            ImageMetadataCondition imgCond => new ConditionEvaluationResult
            {
                ConditionId = imgCond.Id,
                ConditionType = "ImageMetadata",
                IsMatch = imgCond.Evaluate(fileInfo),
                Explanation = $"Image metadata {(imgCond.Evaluate(fileInfo) ? "matched" : "did not match")} specified criteria."
            },
            MediaMetadataCondition mediaCond => new ConditionEvaluationResult
            {
                ConditionId = mediaCond.Id,
                ConditionType = "MediaMetadata",
                IsMatch = mediaCond.Evaluate(fileInfo),
                Explanation = $"Media metadata {(mediaCond.Evaluate(fileInfo) ? "matched" : "did not match")} specified criteria."
            },
            _ => new ConditionEvaluationResult
            {
                ConditionId = condition.Id,
                ConditionType = condition.GetType().Name,
                IsMatch = condition.Evaluate(fileInfo),
                Explanation = $"Evaluated condition of type {condition.GetType().Name}."
            }
        };
    }

    private static ConditionEvaluationResult EvaluateConditionGroup(ConditionGroup group, FileInfo fileInfo)
    {
        var childResults = group.Conditions
            .Select(c => EvaluateCondition(c, fileInfo))
            .ToList();

        bool isMatch = !group.Conditions.Any() || (group.Operator == LogicOperator.And
            ? childResults.All(c => c.IsMatch)
            : childResults.Any(c => c.IsMatch));

        var explanation = group.Operator switch
        {
            LogicOperator.And => isMatch
                ? "All child conditions in AND group were satisfied."
                : $"{childResults.Count(c => !c.IsMatch)} of {childResults.Count} condition(s) in AND group failed.",
            LogicOperator.Or => isMatch
                ? "At least one child condition in OR group was satisfied."
                : "None of the child conditions in OR group were satisfied.",
            _ => isMatch ? "Group matched." : "Group rejected."
        };

        return new ConditionEvaluationResult
        {
            ConditionId = group.Id,
            ConditionType = "ConditionGroup",
            Operator = group.Operator.ToString(),
            TargetValue = $"{group.Operator} ({group.Conditions.Count} conditions)",
            ActualValue = $"{childResults.Count(c => c.IsMatch)} matched / {childResults.Count} total",
            IsMatch = isMatch,
            Explanation = explanation,
            Children = childResults
        };
    }

    private static ConditionEvaluationResult EvaluateFileName(FileNameCondition condition, FileInfo fileInfo)
    {
        var actualName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        var isMatch = condition.Evaluate(fileInfo);

        var explanation = condition.Operator switch
        {
            StringOperator.Is => isMatch
                ? $"File name '{actualName}' equals expected '{condition.Value}'."
                : $"File name '{actualName}' does not equal expected '{condition.Value}'.",
            StringOperator.IsNot => isMatch
                ? $"File name '{actualName}' is not equal to '{condition.Value}'."
                : $"File name '{actualName}' equals '{condition.Value}' (expected different name).",
            StringOperator.Contains => isMatch
                ? $"File name '{actualName}' contains substring '{condition.Value}'."
                : $"File name '{actualName}' does not contain expected substring '{condition.Value}'.",
            StringOperator.DoesNotContain => isMatch
                ? $"File name '{actualName}' does not contain '{condition.Value}'."
                : $"File name '{actualName}' contains forbidden substring '{condition.Value}'.",
            StringOperator.StartsWith => isMatch
                ? $"File name '{actualName}' starts with prefix '{condition.Value}'."
                : $"File name '{actualName}' does not start with expected prefix '{condition.Value}'.",
            StringOperator.EndsWith => isMatch
                ? $"File name '{actualName}' ends with suffix '{condition.Value}'."
                : $"File name '{actualName}' does not end with expected suffix '{condition.Value}'.",
            StringOperator.MatchesRegex => isMatch
                ? $"File name '{actualName}' matches regex pattern '{condition.Value}'."
                : $"File name '{actualName}' does not match regex pattern '{condition.Value}'.",
            StringOperator.IsEmpty => isMatch
                ? "File name is empty."
                : $"File name '{actualName}' is not empty.",
            _ => isMatch ? "File name matched condition." : "File name did not match condition."
        };

        return new ConditionEvaluationResult
        {
            ConditionId = condition.Id,
            ConditionType = "FileName",
            Operator = condition.Operator.ToString(),
            TargetValue = condition.Value,
            ActualValue = actualName,
            IsMatch = isMatch,
            Explanation = explanation
        };
    }

    private static ConditionEvaluationResult EvaluateFileExtension(FileExtensionCondition condition, FileInfo fileInfo)
    {
        var actualExt = fileInfo.Extension.TrimStart('.');
        var targetExt = condition.Value.TrimStart('.');
        var isMatch = condition.Evaluate(fileInfo);

        var explanation = condition.Operator switch
        {
            StringOperator.Is => isMatch
                ? $"Extension '.{actualExt}' equals expected '.{targetExt}'."
                : $"Extension '.{actualExt}' does not match expected '.{targetExt}'.",
            StringOperator.IsNot => isMatch
                ? $"Extension '.{actualExt}' is not equal to '.{targetExt}'."
                : $"Extension '.{actualExt}' equals forbidden '.{targetExt}'.",
            StringOperator.Contains => isMatch
                ? $"Extension '.{actualExt}' contains '{targetExt}'."
                : $"Extension '.{actualExt}' does not contain '{targetExt}'.",
            _ => isMatch ? "Extension matched condition." : "Extension did not match condition."
        };

        return new ConditionEvaluationResult
        {
            ConditionId = condition.Id,
            ConditionType = "FileExtension",
            Operator = condition.Operator.ToString(),
            TargetValue = $".{targetExt}",
            ActualValue = $".{actualExt}",
            IsMatch = isMatch,
            Explanation = explanation
        };
    }

    private static ConditionEvaluationResult EvaluateFileSize(FileSizeCondition condition, FileInfo fileInfo)
    {
        var actualBytes = fileInfo.Exists ? fileInfo.Length : 0;
        var actualFormatted = FormatBytes(actualBytes);
        var targetFormatted = FormatBytes(condition.Value);
        var isMatch = condition.Evaluate(fileInfo);

        var explanation = condition.Operator switch
        {
            FileSizeCondition.SizeOperator.Equals => isMatch
                ? $"File size {actualFormatted} equals target {targetFormatted}."
                : $"File size {actualFormatted} does not equal target {targetFormatted}.",
            FileSizeCondition.SizeOperator.NotEquals => isMatch
                ? $"File size {actualFormatted} is not equal to {targetFormatted}."
                : $"File size {actualFormatted} equals forbidden size {targetFormatted}.",
            FileSizeCondition.SizeOperator.GreaterThan => isMatch
                ? $"File size {actualFormatted} is greater than {targetFormatted}."
                : $"File size {actualFormatted} is not greater than {targetFormatted}.",
            FileSizeCondition.SizeOperator.LessThan => isMatch
                ? $"File size {actualFormatted} is less than {targetFormatted}."
                : $"File size {actualFormatted} is not less than {targetFormatted}.",
            FileSizeCondition.SizeOperator.Between => isMatch
                ? $"File size {actualFormatted} is between {targetFormatted} and {FormatBytes(condition.MaxValue ?? long.MaxValue)}."
                : $"File size {actualFormatted} is outside range [{targetFormatted} - {FormatBytes(condition.MaxValue ?? long.MaxValue)}].",
            _ => isMatch ? "File size matched." : "File size did not match."
        };

        var targetDisplay = condition.Operator == FileSizeCondition.SizeOperator.Between
            ? $"{targetFormatted} - {FormatBytes(condition.MaxValue ?? long.MaxValue)}"
            : targetFormatted;

        return new ConditionEvaluationResult
        {
            ConditionId = condition.Id,
            ConditionType = "FileSize",
            Operator = condition.Operator.ToString(),
            TargetValue = targetDisplay,
            ActualValue = actualFormatted,
            IsMatch = isMatch,
            Explanation = explanation
        };
    }

    private static ConditionEvaluationResult EvaluateFileDate(FileDateCondition condition, FileInfo fileInfo)
    {
        var typeName = condition.Type == FileDateCondition.DateType.Created ? "Created" : "Modified";
        var actualDate = condition.Type == FileDateCondition.DateType.Created
            ? fileInfo.CreationTime
            : fileInfo.LastWriteTime;

        var isMatch = condition.Evaluate(fileInfo);

        var explanation = condition.Operator switch
        {
            FileDateCondition.DateOperator.Is => isMatch
                ? $"{typeName} date {actualDate:yyyy-MM-dd} matches {condition.Value:yyyy-MM-dd}."
                : $"{typeName} date {actualDate:yyyy-MM-dd} does not match {condition.Value:yyyy-MM-dd}.",
            FileDateCondition.DateOperator.Before => isMatch
                ? $"{typeName} date {actualDate:yyyy-MM-dd HH:mm} is before {condition.Value:yyyy-MM-dd HH:mm}."
                : $"{typeName} date {actualDate:yyyy-MM-dd HH:mm} is not before {condition.Value:yyyy-MM-dd HH:mm}.",
            FileDateCondition.DateOperator.After => isMatch
                ? $"{typeName} date {actualDate:yyyy-MM-dd HH:mm} is after {condition.Value:yyyy-MM-dd HH:mm}."
                : $"{typeName} date {actualDate:yyyy-MM-dd HH:mm} is not after {condition.Value:yyyy-MM-dd HH:mm}.",
            FileDateCondition.DateOperator.Between => isMatch
                ? $"{typeName} date {actualDate:yyyy-MM-dd} is between {condition.Value:yyyy-MM-dd} and {condition.MaxValue:yyyy-MM-dd}."
                : $"{typeName} date {actualDate:yyyy-MM-dd} is outside range [{condition.Value:yyyy-MM-dd} - {condition.MaxValue:yyyy-MM-dd}].",
            FileDateCondition.DateOperator.OlderThanDays => isMatch
                ? $"{typeName} date ({(int)(DateTime.Now - actualDate).TotalDays} days ago) is older than {condition.DaysOld ?? 0} days."
                : $"{typeName} date ({(int)(DateTime.Now - actualDate).TotalDays} days ago) is not older than {condition.DaysOld ?? 0} days.",
            _ => isMatch ? $"{typeName} date matched." : $"{typeName} date did not match."
        };

        var targetDisplay = condition.Operator switch
        {
            FileDateCondition.DateOperator.Between => $"{condition.Value:yyyy-MM-dd} to {condition.MaxValue:yyyy-MM-dd}",
            FileDateCondition.DateOperator.OlderThanDays => $"> {condition.DaysOld ?? 0} days old",
            _ => condition.Value.ToString("yyyy-MM-dd HH:mm")
        };

        return new ConditionEvaluationResult
        {
            ConditionId = condition.Id,
            ConditionType = "FileDate",
            Operator = $"{condition.Type} {condition.Operator}",
            TargetValue = targetDisplay,
            ActualValue = actualDate.ToString("yyyy-MM-dd HH:mm:ss"),
            IsMatch = isMatch,
            Explanation = explanation
        };
    }

    private static string GetRejectionSummary(ConditionEvaluationResult groupResult, LogicOperator op)
    {
        if (op == LogicOperator.Or)
        {
            return "none of the OR conditions were satisfied.";
        }

        var firstFailure = groupResult.Children.FirstOrDefault(c => !c.IsMatch);
        return firstFailure != null
            ? firstFailure.Explanation
            : "one or more conditions were not satisfied.";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
