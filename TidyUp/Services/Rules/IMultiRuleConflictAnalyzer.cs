using TidyUp.Models.Domain;

namespace TidyUp.Services.Rules;

/// <summary>
/// Severity level of a detected multi-rule conflict.
/// </summary>
public enum RuleConflictSeverity
{
    Warning,
    Error
}

/// <summary>
/// Type classification of multi-rule conflicts.
/// </summary>
public enum RuleConflictType
{
    /// <summary>
    /// Two or more rules match identical file patterns in the same folder with conflicting destinations.
    /// </summary>
    CompetingDestinations,

    /// <summary>
    /// A lower-priority rule will never trigger because a prior rule absorbs or halts processing for those files.
    /// </summary>
    ShadowedRule,

    /// <summary>
    /// Multiple rules execute identical actions redundantly.
    /// </summary>
    RedundantAction
}

/// <summary>
/// Represents a detected conflict between two rules.
/// </summary>
public class RuleConflict
{
    public Guid PrimaryRuleId { get; set; }
    public string PrimaryRuleName { get; set; } = string.Empty;
    public Guid SecondaryRuleId { get; set; }
    public string SecondaryRuleName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public RuleConflictType ConflictType { get; set; }
    public RuleConflictSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// Consolidated diagnostic report analyzing interactions across all active rules.
/// </summary>
public class MultiRuleConflictReport
{
    public List<RuleConflict> Conflicts { get; set; } = new();
    public int TotalRulesAnalyzed { get; set; }
    public bool HasConflicts => Conflicts.Count > 0;
    public int CompetingDestinationCount => Conflicts.Count(c => c.ConflictType == RuleConflictType.CompetingDestinations);
    public int ShadowedRuleCount => Conflicts.Count(c => c.ConflictType == RuleConflictType.ShadowedRule);
}

/// <summary>
/// Service interface for analyzing multi-rule interactions, conflicting destinations, and shadowing.
/// </summary>
public interface IMultiRuleConflictAnalyzer
{
    /// <summary>
    /// Evaluates a collection of rules for inter-rule conflicts, shadowing, and competing destinations.
    /// </summary>
    MultiRuleConflictReport AnalyzeRules(IEnumerable<Rule> rules);
}

