using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data;
using TidyUp.Data.Entities;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;

namespace TidyUp.Services.Rules;

/// <summary>
/// Data contract used to serialize an immutable rule snapshot into JSON.
/// </summary>
public class RuleSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public int ExecutionOrder { get; set; }
    public bool StopProcessingAfterMatch { get; set; }
    public List<MonitoredFolder> MonitoredFolders { get; set; } = new();
    public ConditionGroup? Conditions { get; set; }
    public List<FileAction> Actions { get; set; } = new();

    public static RuleSnapshot FromRule(Rule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Description = rule.Description,
        IsEnabled = rule.IsEnabled,
        ExecutionOrder = rule.ExecutionOrder,
        StopProcessingAfterMatch = rule.StopProcessingAfterMatch,
        MonitoredFolders = rule.MonitoredFolders.ToList(),
        Conditions = rule.Conditions,
        Actions = rule.Actions.ToList()
    };

    public Rule ToRule() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        IsEnabled = IsEnabled,
        ExecutionOrder = ExecutionOrder,
        StopProcessingAfterMatch = StopProcessingAfterMatch,
        MonitoredFolders = new ObservableCollection<MonitoredFolder>(MonitoredFolders),
        Conditions = Conditions,
        Actions = new ObservableCollection<FileAction>(Actions)
    };
}

/// <summary>
/// Service implementation managing versioned rule revisions, diffing, and rollbacks.
/// </summary>
public class RuleRevisionManager : IRuleRevisionManager
{
    private readonly TidyUpDbContext _context;
    private readonly IRuleRepository? _ruleRepository;
    private readonly IRuleSummaryGenerator? _summaryGenerator;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RuleRevisionManager(
        TidyUpDbContext context,
        IRuleRepository? ruleRepository = null,
        IRuleSummaryGenerator? summaryGenerator = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _ruleRepository = ruleRepository;
        _summaryGenerator = summaryGenerator;
    }

    public async Task<RuleRevision> SaveRevisionAsync(Rule rule, string? changeDescription = null)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var latest = await _context.RuleRevisions
            .Where(r => r.RuleId == rule.Id)
            .OrderByDescending(r => r.VersionNumber)
            .FirstOrDefaultAsync();

        int nextVersion = (latest?.VersionNumber ?? 0) + 1;

        var snapshot = RuleSnapshot.FromRule(rule);
        string json = JsonSerializer.Serialize(snapshot, JsonOptions);

        var revision = new RuleRevision
        {
            RevisionId = Guid.NewGuid(),
            RuleId = rule.Id,
            VersionNumber = nextVersion,
            RuleName = rule.Name,
            Description = rule.Description,
            SerializedRuleJson = json,
            CreatedAtUtc = DateTime.UtcNow,
            ChangeDescription = changeDescription ?? (nextVersion == 1 ? "Initial configuration" : "Rule updated")
        };

        _context.RuleRevisions.Add(revision);
        await _context.SaveChangesAsync();

        return revision;
    }

    public async Task<List<RuleRevision>> GetRevisionsForRuleAsync(Guid ruleId)
    {
        return await _context.RuleRevisions
            .Where(r => r.RuleId == ruleId)
            .OrderByDescending(r => r.VersionNumber)
            .ToListAsync();
    }

    public async Task<Rule> RestoreRevisionAsync(Guid revisionId)
    {
        var revision = await _context.RuleRevisions.FindAsync(revisionId)
            ?? throw new KeyNotFoundException($"Rule revision '{revisionId}' was not found.");

        var snapshot = JsonSerializer.Deserialize<RuleSnapshot>(revision.SerializedRuleJson, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize rule revision snapshot.");

        var restoredRule = snapshot.ToRule();

        if (_ruleRepository != null)
        {
            await _ruleRepository.UpdateAsync(restoredRule);
        }

        // Record a new revision snapshot documenting the restoration
        await SaveRevisionAsync(restoredRule, $"Restored from Version {revision.VersionNumber}");

        return restoredRule;
    }

    public RuleDiffResult DiffRevisions(RuleRevision fromRevision, RuleRevision toRevision)
    {
        ArgumentNullException.ThrowIfNull(fromRevision);
        ArgumentNullException.ThrowIfNull(toRevision);

        var fromSnap = JsonSerializer.Deserialize<RuleSnapshot>(fromRevision.SerializedRuleJson, JsonOptions) ?? new RuleSnapshot();
        var toSnap = JsonSerializer.Deserialize<RuleSnapshot>(toRevision.SerializedRuleJson, JsonOptions) ?? new RuleSnapshot();

        var diff = new RuleDiffResult
        {
            RuleId = fromRevision.RuleId,
            OldVersion = fromRevision.VersionNumber,
            NewVersion = toRevision.VersionNumber,
            HasNameChange = !string.Equals(fromSnap.Name, toSnap.Name, StringComparison.Ordinal),
            OldName = fromSnap.Name,
            NewName = toSnap.Name,
            HasStatusChange = fromSnap.IsEnabled != toSnap.IsEnabled,
            OldIsEnabled = fromSnap.IsEnabled,
            NewIsEnabled = toSnap.IsEnabled
        };

        // Folders diff
        var fromFolders = fromSnap.MonitoredFolders.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toFolders = toSnap.MonitoredFolders.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        diff.AddedFolders = toFolders.Except(fromFolders).ToList();
        diff.RemovedFolders = fromFolders.Except(toFolders).ToList();

        // Conditions diff
        string fromCond = _summaryGenerator?.GenerateConditionSummary(fromSnap.Conditions) ?? JsonSerializer.Serialize(fromSnap.Conditions, JsonOptions);
        string toCond = _summaryGenerator?.GenerateConditionSummary(toSnap.Conditions) ?? JsonSerializer.Serialize(toSnap.Conditions, JsonOptions);

        if (!string.Equals(fromCond, toCond, StringComparison.Ordinal))
        {
            diff.HasConditionChange = true;
            diff.OldConditionsSummary = fromCond;
            diff.NewConditionsSummary = toCond;
        }

        // Actions diff
        var fromActions = fromSnap.Actions.Select(DescribeAction).ToList();
        var toActions = toSnap.Actions.Select(DescribeAction).ToList();

        diff.AddedActions = toActions.Except(fromActions).ToList();
        diff.RemovedActions = fromActions.Except(toActions).ToList();

        return diff;
    }

    private static string DescribeAction(FileAction action)
    {
        return action switch
        {
            MoveFileAction m => $"Move to '{m.DestinationPath}'",
            CopyFileAction c => $"Copy to '{c.DestinationPath}'",
            RenameFileAction r => $"Rename with pattern '{r.NamePattern}'",
            ChangeExtensionAction e => $"Change extension to '{e.NewExtension}'",
            DeleteFileAction d => d.UseRecycleBin ? "Recycle Bin Delete" : "Permanent Delete",
            ExtractArchiveAction ext => $"Extract archive to '{ext.DestinationPath}'",
            RunCommandAction cmd => $"Run '{cmd.Command}'",
            _ => action.GetType().Name
        };
    }
}
