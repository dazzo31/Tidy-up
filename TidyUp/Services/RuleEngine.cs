using System.IO;
using TidyUp.Models.Domain;

namespace TidyUp.Services;

/// <summary>
/// Implementation of rule evaluation engine.
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
}
