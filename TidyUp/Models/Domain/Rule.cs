using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TidyUp.Models.Enums;

namespace TidyUp.Models.Domain;

/// <summary>
/// Represents a file organization rule with conditions and actions.
/// </summary>
public partial class Rule : ObservableObject
{
    /// <summary>
    /// Unique identifier for the rule.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Name of the rule.
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Description of what the rule does.
    /// </summary>
    [ObservableProperty]
    private string? _description;

    /// <summary>
    /// Folders to monitor for file changes.
    /// </summary>
    public ObservableCollection<MonitoredFolder> MonitoredFolders { get; set; } = new();

    /// <summary>
    /// Root condition group that defines when this rule matches.
    /// </summary>
    [ObservableProperty]
    private ConditionGroup? _conditions;

    /// <summary>
    /// Actions to execute when the rule matches, in order.
    /// </summary>
    public ObservableCollection<FileAction> Actions { get; set; } = new();

    /// <summary>
    /// Whether the rule is currently enabled.
    /// Defaults to false per safe-by-default design principle.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = false;

    /// <summary>
    /// Execution order priority (lower numbers execute first).
    /// </summary>
    [ObservableProperty]
    private int _executionOrder;

    /// <summary>
    /// If true, stop processing additional rules for files that match this rule.
    /// </summary>
    [ObservableProperty]
    private bool _stopProcessingAfterMatch;

    /// <summary>
    /// Trigger mode for the rule (Continuous, Scheduled, ManualOnly).
    /// </summary>
    [ObservableProperty]
    private RuleTriggerType _triggerType = RuleTriggerType.Continuous;

    /// <summary>
    /// Scheduled time of day when trigger mode is Scheduled.
    /// </summary>
    [ObservableProperty]
    private TimeSpan? _scheduledTime;

    /// <summary>
    /// When the rule was created.
    /// </summary>
    [ObservableProperty]
    private DateTime _createdDate = DateTime.UtcNow;

    /// <summary>
    /// When the rule was last modified.
    /// </summary>
    [ObservableProperty]
    private DateTime _modifiedDate = DateTime.UtcNow;

    /// <summary>
    /// When the rule was last executed.
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastRunDate;

    /// <summary>
    /// Total number of files processed by this rule.
    /// </summary>
    [ObservableProperty]
    private int _filesProcessedCount;

    /// <summary>
    /// Plain-language natural language summary of this rule's behavior.
    /// </summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>"{n} folder(s) \u00b7 {summary}" \u2014 what the rule list shows under the name.</summary>
    public string ListSubtitle
    {
        get
        {
            var folderCount = MonitoredFolders?.Count ?? 0;
            var folders = folderCount == 1 ? "1 folder" : $"{folderCount} folders";
            return string.IsNullOrWhiteSpace(Summary) ? folders : $"{folders} \u00b7 {Summary}";
        }
    }

    /// <summary>Explains the priority badge, including the last-run file count when there is one.</summary>
    public string ExecutionOrderTooltip => LastRunDate is { } lastRun
        ? $"Execution priority {ExecutionOrder} (lower runs first). Last run {lastRun.ToLocalTime():g} processed {FilesProcessedCount} file(s)."
        : $"Execution priority {ExecutionOrder} (lower runs first). No files matched on the last run.";

    /// <summary>
    /// Whether this rule has unsaved changes.
    /// </summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>
    /// Whether this rule currently has validation errors or invalid configuration.
    /// </summary>
    [ObservableProperty]
    private bool _hasErrors;

    /// <summary>
    /// Validation error message when HasErrors is true.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Creates an independent deep clone of this Rule entity.
    /// </summary>
    public Rule DeepClone() => RuleDraft.DeepCloneRule(this);
}

/// <summary>
/// Represents a folder being monitored by a rule.
/// </summary>
public partial class MonitoredFolder : ObservableObject
{
    /// <summary>
    /// Path to the folder.
    /// </summary>
    [ObservableProperty]
    private string _path = string.Empty;

    /// <summary>
    /// Whether to include subfolders in monitoring.
    /// </summary>
    [ObservableProperty]
    private bool _includeSubfolders = true;

    /// <summary>
    /// Patterns to exclude from monitoring (e.g., "node_modules", ".git").
    /// </summary>
    public ObservableCollection<string> ExclusionPatterns { get; set; } = new();

    /// <summary>
    /// Helper property for UI binding - comma-separated exclusion patterns.
    /// </summary>
    public string ExclusionPatternsText
    {
        get => string.Join(", ", ExclusionPatterns);
        set
        {
            ExclusionPatterns.Clear();
            if (!string.IsNullOrWhiteSpace(value))
            {
                foreach (var pattern in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    ExclusionPatterns.Add(pattern);
                }
            }
            OnPropertyChanged(nameof(ExclusionPatternsText));
        }
    }
}
