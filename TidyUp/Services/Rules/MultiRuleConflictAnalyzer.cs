using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.Services.Rules;

/// <summary>
/// Analyzes interactions between multiple rules to identify competing destinations and shadowed rules.
/// </summary>
public class MultiRuleConflictAnalyzer : IMultiRuleConflictAnalyzer
{
    public MultiRuleConflictReport AnalyzeRules(IEnumerable<Rule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var activeRules = rules.Where(r => r.IsEnabled).OrderBy(r => r.ExecutionOrder).ToList();
        var report = new MultiRuleConflictReport
        {
            TotalRulesAnalyzed = activeRules.Count
        };

        if (activeRules.Count < 2)
        {
            return report;
        }

        for (int i = 0; i < activeRules.Count; i++)
        {
            var ruleA = activeRules[i];

            for (int j = i + 1; j < activeRules.Count; j++)
            {
                var ruleB = activeRules[j];

                AnalyzePair(ruleA, ruleB, report);
            }
        }

        return report;
    }

    private void AnalyzePair(Rule rulePrior, Rule ruleSubsequent, MultiRuleConflictReport report)
    {
        var sharedFolders = FindSharedFolders(rulePrior, ruleSubsequent);
        if (sharedFolders.Count == 0)
            return;

        foreach (var folder in sharedFolders)
        {
            var extensionsA = ExtractExtensionFilters(rulePrior);
            var extensionsB = ExtractExtensionFilters(ruleSubsequent);

            bool overlappingPattern = ArePatternsOverlapping(extensionsA, extensionsB);
            if (!overlappingPattern)
                continue;

            var destPrior = GetPrimaryDestination(rulePrior);
            var destSubsequent = GetPrimaryDestination(ruleSubsequent);

            // 1. Competing Destinations: Both rules match in the same folder, but move/copy to different locations
            if (!string.IsNullOrEmpty(destPrior) &&
                !string.IsNullOrEmpty(destSubsequent) &&
                !string.Equals(NormalizePath(destPrior), NormalizePath(destSubsequent), StringComparison.OrdinalIgnoreCase))
            {
                report.Conflicts.Add(new RuleConflict
                {
                    PrimaryRuleId = rulePrior.Id,
                    PrimaryRuleName = rulePrior.Name,
                    SecondaryRuleId = ruleSubsequent.Id,
                    SecondaryRuleName = ruleSubsequent.Name,
                    FolderPath = folder,
                    ConflictType = RuleConflictType.CompetingDestinations,
                    Severity = RuleConflictSeverity.Error,
                    Description = $"Rule '{rulePrior.Name}' and Rule '{ruleSubsequent.Name}' both match in '{folder}' but route files to different destinations ('{destPrior}' vs '{destSubsequent}').",
                    Recommendation = "Refine file conditions so files match only one rule, or harmonize the target destinations."
                });
            }

            // 2. Shadowed Rule: rulePrior runs first, moves/deletes or has StopProcessingAfterMatch=true,
            // and rulePrior's filter subsumes or matches ruleSubsequent's filter.
            bool priorConsumesFile = rulePrior.StopProcessingAfterMatch || HasConsumingAction(rulePrior);
            if (priorConsumesFile && IsSubsumedBy(extensionsB, extensionsA))
            {
                report.Conflicts.Add(new RuleConflict
                {
                    PrimaryRuleId = rulePrior.Id,
                    PrimaryRuleName = rulePrior.Name,
                    SecondaryRuleId = ruleSubsequent.Id,
                    SecondaryRuleName = ruleSubsequent.Name,
                    FolderPath = folder,
                    ConflictType = RuleConflictType.ShadowedRule,
                    Severity = RuleConflictSeverity.Warning,
                    Description = $"Rule '{ruleSubsequent.Name}' is shadowed by higher-priority Rule '{rulePrior.Name}'. Files in '{folder}' matching this rule will always be caught by '{rulePrior.Name}' first.",
                    Recommendation = $"Reorder execution priority so '{ruleSubsequent.Name}' executes before '{rulePrior.Name}', or make '{rulePrior.Name}' conditions more specific."
                });
            }
        }
    }

    private static List<string> FindSharedFolders(Rule a, Rule b)
    {
        var shared = new List<string>();

        foreach (var folderA in a.MonitoredFolders)
        {
            var normA = NormalizePath(folderA.Path);

            foreach (var folderB in b.MonitoredFolders)
            {
                var normB = NormalizePath(folderB.Path);

                if (string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase))
                {
                    if (!shared.Contains(normA, StringComparer.OrdinalIgnoreCase))
                        shared.Add(normA);
                }
                else if (folderA.IncludeSubfolders && normB.StartsWith(normA + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    if (!shared.Contains(normB, StringComparer.OrdinalIgnoreCase))
                        shared.Add(normB);
                }
                else if (folderB.IncludeSubfolders && normA.StartsWith(normB + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    if (!shared.Contains(normA, StringComparer.OrdinalIgnoreCase))
                        shared.Add(normA);
                }
            }
        }

        return shared;
    }

    private static HashSet<string>? ExtractExtensionFilters(Rule rule)
    {
        if (rule.Conditions == null || rule.Conditions.Conditions.Count == 0)
            return null; // Null signifies matching any/all extensions

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectExtensions(rule.Conditions, extensions);

        return extensions.Count == 0 ? null : extensions;
    }

    private static void CollectExtensions(ConditionGroup group, HashSet<string> extensions)
    {
        foreach (var cond in group.Conditions)
        {
            if (cond is FileExtensionCondition extCond && !string.IsNullOrWhiteSpace(extCond.Value))
            {
                var clean = extCond.Value.TrimStart('.').ToLowerInvariant();
                extensions.Add(clean);
            }
            else if (cond is ConditionGroup subGroup)
            {
                CollectExtensions(subGroup, extensions);
            }
        }
    }

    private static bool ArePatternsOverlapping(HashSet<string>? extsA, HashSet<string>? extsB)
    {
        // Null means rule matches all extensions
        if (extsA == null || extsB == null)
            return true;

        return extsA.Overlaps(extsB);
    }

    private static bool IsSubsumedBy(HashSet<string>? candidate, HashSet<string>? parent)
    {
        // If parent matches all extensions, candidate is subsumed
        if (parent == null)
            return true;

        // If candidate matches all extensions but parent is restricted, candidate is not subsumed
        if (candidate == null)
            return false;

        // Candidate is subsumed if all its extensions are covered by parent
        return candidate.IsSubsetOf(parent);
    }

    private static string? GetPrimaryDestination(Rule rule)
    {
        foreach (var action in rule.Actions)
        {
            if (action is MoveFileAction move && !string.IsNullOrWhiteSpace(move.DestinationPath))
                return move.DestinationPath;

            if (action is CopyFileAction copy && !string.IsNullOrWhiteSpace(copy.DestinationPath))
                return copy.DestinationPath;
        }

        return null;
    }

    private static bool HasConsumingAction(Rule rule)
    {
        return rule.Actions.Any(a => a is MoveFileAction or DeleteFileAction);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return Path.GetFullPath(path).TrimEnd('\\', '/');
    }
}
