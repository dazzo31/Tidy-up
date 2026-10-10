using System.IO;
using System.Text;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.Services.Rules;

/// <summary>
/// Default implementation of natural-language rule summary generator.
/// Translates conditions, folders, and actions into plain-language prose.
/// </summary>
public class RuleSummaryGenerator : IRuleSummaryGenerator
{
    public string GenerateSummary(Rule rule)
    {
        if (rule == null)
            return "No rule provided.";

        var sb = new StringBuilder();

        // 1. Trigger Clause (When a file...)
        sb.Append("When a file");

        // 2. Conditions Clause
        var conditionSummary = GenerateConditionSummary(rule.Conditions);
        if (!string.IsNullOrWhiteSpace(conditionSummary))
        {
            sb.Append(" where ");
            sb.Append(conditionSummary);
        }

        // 3. Monitored Folders Clause
        if (rule.MonitoredFolders.Count > 0)
        {
            var validFolders = rule.MonitoredFolders
                .Where(f => !string.IsNullOrWhiteSpace(f.Path))
                .Select(f =>
                {
                    var name = Path.GetFileName(f.Path);
                    return string.IsNullOrEmpty(name) ? f.Path : name;
                })
                .Distinct()
                .ToList();

            if (validFolders.Count == 1)
            {
                sb.Append($" is detected in '{validFolders[0]}'");
            }
            else if (validFolders.Count > 1)
            {
                var folderList = string.Join("', '", validFolders.Take(validFolders.Count - 1));
                sb.Append($" is detected in '{folderList}' or '{validFolders.Last()}'");
            }
            else
            {
                sb.Append(" is detected");
            }
        }
        else
        {
            sb.Append(" is detected");
        }

        // 4. Actions Clause
        if (rule.Actions.Count == 0)
        {
            sb.Append(", no actions are executed.");
            return sb.ToString();
        }

        sb.Append(", ");
        var actionSummaries = rule.Actions
            .OrderBy(a => a.Order)
            .Select(GenerateActionSummary)
            .ToList();

        if (actionSummaries.Count == 1)
        {
            sb.Append(actionSummaries[0]);
        }
        else if (actionSummaries.Count == 2)
        {
            sb.Append($"{actionSummaries[0]} and {actionSummaries[1]}");
        }
        else
        {
            var leading = string.Join(", ", actionSummaries.Take(actionSummaries.Count - 1));
            sb.Append($"{leading}, and {actionSummaries.Last()}");
        }

        sb.Append('.');
        return sb.ToString();
    }

    public string GenerateConditionSummary(ConditionGroup? group)
    {
        if (group == null || group.Conditions.Count == 0)
            return string.Empty;

        var parts = new List<string>();

        foreach (var condition in group.Conditions)
        {
            var summary = SummarizeCondition(condition);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                parts.Add(summary);
            }
        }

        if (parts.Count == 0)
            return string.Empty;

        var conjunction = group.Operator == LogicOperator.Or ? " or " : " and ";
        return string.Join(conjunction, parts);
    }

    private string SummarizeCondition(Condition condition)
    {
        return condition switch
        {
            FileExtensionCondition ext => SummarizeExtensionCondition(ext),
            FileNameCondition name => SummarizeNameCondition(name),
            FileSizeCondition size => SummarizeSizeCondition(size),
            FileDateCondition date => SummarizeDateCondition(date),
            FileContentCondition content => $"content contains '{content.SearchText}'",
            ImageMetadataCondition img => "image matches metadata criteria",
            MediaMetadataCondition media => "media matches duration/metadata criteria",
            ConditionGroup nested => SummarizeNestedGroup(nested),
            _ => "custom condition is met"
        };
    }

    private string SummarizeNestedGroup(ConditionGroup group)
    {
        var inner = GenerateConditionSummary(group);
        if (string.IsNullOrWhiteSpace(inner))
            return string.Empty;

        return group.Conditions.Count > 1 ? $"({inner})" : inner;
    }

    private static string SummarizeExtensionCondition(FileExtensionCondition cond)
    {
        var ext = cond.Value.TrimStart('.');
        return cond.Operator switch
        {
            StringOperator.Is => $"extension is '{ext}'",
            StringOperator.IsNot => $"extension is not '{ext}'",
            StringOperator.Contains => $"extension contains '{ext}'",
            _ => $"extension is '{ext}'"
        };
    }

    private static string SummarizeNameCondition(FileNameCondition cond)
    {
        return cond.Operator switch
        {
            StringOperator.Is => $"name is '{cond.Value}'",
            StringOperator.IsNot => $"name is not '{cond.Value}'",
            StringOperator.Contains => $"name contains '{cond.Value}'",
            StringOperator.DoesNotContain => $"name does not contain '{cond.Value}'",
            StringOperator.StartsWith => $"name starts with '{cond.Value}'",
            StringOperator.EndsWith => $"name ends with '{cond.Value}'",
            StringOperator.MatchesRegex => $"name matches regex '{cond.Value}'",
            StringOperator.IsEmpty => "name is empty",
            _ => $"name is '{cond.Value}'"
        };
    }

    private static string SummarizeSizeCondition(FileSizeCondition cond)
    {
        var formatted = FormatFileSize(cond.Value);
        return cond.Operator switch
        {
            FileSizeCondition.SizeOperator.Equals => $"size is exactly {formatted}",
            FileSizeCondition.SizeOperator.NotEquals => $"size is not {formatted}",
            FileSizeCondition.SizeOperator.GreaterThan => $"size is greater than {formatted}",
            FileSizeCondition.SizeOperator.LessThan => $"size is less than {formatted}",
            FileSizeCondition.SizeOperator.Between => $"size is between {formatted} and {FormatFileSize(cond.MaxValue ?? cond.Value)}",
            _ => $"size is {formatted}"
        };
    }

    private static string SummarizeDateCondition(FileDateCondition cond)
    {
        var dateField = cond.Type == FileDateCondition.DateType.Created ? "created date" : "modified date";
        return cond.Operator switch
        {
            FileDateCondition.DateOperator.Is => $"{dateField} is on {cond.Value:yyyy-MM-dd}",
            FileDateCondition.DateOperator.Before => $"{dateField} is before {cond.Value:yyyy-MM-dd}",
            FileDateCondition.DateOperator.After => $"{dateField} is after {cond.Value:yyyy-MM-dd}",
            FileDateCondition.DateOperator.Between => $"{dateField} is between {cond.Value:yyyy-MM-dd} and {(cond.MaxValue.HasValue ? cond.MaxValue.Value.ToString("yyyy-MM-dd") : "now")}",
            FileDateCondition.DateOperator.OlderThanDays => $"{dateField} is older than {cond.DaysOld ?? 0} days",
            _ => $"{dateField} matches date criteria"
        };
    }

    public string GenerateActionSummary(FileAction action)
    {
        return action switch
        {
            MoveFileAction move => SummarizeMoveAction(move),
            CopyFileAction copy => SummarizeCopyAction(copy),
            RenameFileAction rename => SummarizeRenameAction(rename),
            ChangeExtensionAction ext => $"change extension to '.{ext.NewExtension.TrimStart('.')}'",
            DeleteFileAction del => SummarizeDeleteAction(del),
            ExtractArchiveAction extract => $"extract archive contents to '{GetPathDisplayName(extract.DestinationPath)}'",
            RunCommandAction cmd => $"run command '{cmd.Command}'",
            _ => action.GetType().Name
        };
    }

    private static string SummarizeMoveAction(MoveFileAction move)
    {
        var conflict = move.ConflictResolution switch
        {
            ConflictResolution.Overwrite => " (overwriting if exists)",
            ConflictResolution.Skip => " (skipping if conflict)",
            ConflictResolution.RenameNew => " (renaming if conflict)",
            _ => string.Empty
        };

        return $"move to '{GetPathDisplayName(move.DestinationPath)}'{conflict}";
    }

    private static string SummarizeCopyAction(CopyFileAction copy)
    {
        var conflict = copy.ConflictResolution switch
        {
            ConflictResolution.Overwrite => " (overwriting if exists)",
            ConflictResolution.Skip => " (skipping if conflict)",
            ConflictResolution.RenameNew => " (renaming if conflict)",
            _ => string.Empty
        };

        return $"copy to '{GetPathDisplayName(copy.DestinationPath)}'{conflict}";
    }

    private static string SummarizeRenameAction(RenameFileAction rename)
    {
        return $"rename using pattern '{rename.NamePattern}'";
    }

    private static string SummarizeDeleteAction(DeleteFileAction del)
    {
        var emptyFolders = del.RemoveEmptyFolders ? " and remove empty parent folders" : string.Empty;
        return del.UseRecycleBin
            ? $"send to Recycle Bin{emptyFolders}"
            : $"permanently delete{emptyFolders}";
    }

    private static string GetPathDisplayName(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "(not set)";
        var dirName = Path.GetFileName(path.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(dirName) ? path : dirName;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        if (bytes < 1024L * 1024L * 1024L) return $"{(bytes / (1024.0 * 1024.0)):F1} MB";
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F1} GB";
    }
}

