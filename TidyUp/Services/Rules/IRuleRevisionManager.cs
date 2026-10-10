using TidyUp.Data.Entities;
using TidyUp.Models.Domain;

namespace TidyUp.Services.Rules;

/// <summary>
/// Summary of changes detected between two historical rule revisions.
/// </summary>
public class RuleDiffResult
{
    public Guid RuleId { get; set; }
    public int OldVersion { get; set; }
    public int NewVersion { get; set; }

    public bool HasNameChange { get; set; }
    public string? OldName { get; set; }
    public string? NewName { get; set; }

    public bool HasStatusChange { get; set; }
    public bool OldIsEnabled { get; set; }
    public bool NewIsEnabled { get; set; }

    public List<string> AddedFolders { get; set; } = new();
    public List<string> RemovedFolders { get; set; } = new();

    public bool HasConditionChange { get; set; }
    public string? OldConditionsSummary { get; set; }
    public string? NewConditionsSummary { get; set; }

    public List<string> AddedActions { get; set; } = new();
    public List<string> RemovedActions { get; set; } = new();

    public bool HasChanges => HasNameChange ||
                              HasStatusChange ||
                              HasConditionChange ||
                              AddedFolders.Count > 0 ||
                              RemovedFolders.Count > 0 ||
                              AddedActions.Count > 0 ||
                              RemovedActions.Count > 0;
}

/// <summary>
/// Service interface for managing versioned rule revisions, side-by-side diffing, and restoration.
/// </summary>
public interface IRuleRevisionManager
{
    /// <summary>
    /// Captures and persists an immutable revision snapshot of the specified rule.
    /// </summary>
    Task<RuleRevision> SaveRevisionAsync(Rule rule, string? changeDescription = null);

    /// <summary>
    /// Retrieves all historical revisions for a rule, ordered from newest to oldest.
    /// </summary>
    Task<List<RuleRevision>> GetRevisionsForRuleAsync(Guid ruleId);

    /// <summary>
    /// Restores a past rule revision by ID, writing its configuration back to the rule repository.
    /// </summary>
    Task<Rule> RestoreRevisionAsync(Guid revisionId);

    /// <summary>
    /// Performs a side-by-side configuration diff between two revisions.
    /// </summary>
    RuleDiffResult DiffRevisions(RuleRevision fromRevision, RuleRevision toRevision);
}

