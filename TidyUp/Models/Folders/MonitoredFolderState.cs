using TidyUp.Services.Folders;
using TidyUp.Services.Monitoring;

namespace TidyUp.Models.Folders;

/// <summary>
/// Unified representation of a monitored folder across Rules, Folders Hub, and Dashboard.
/// Single source of truth containing canonical path, health status, and linked rules.
/// </summary>
public class MonitoredFolderState
{
    public string NormalizedPath { get; init; } = string.Empty;
    public string DisplayPath { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public bool HasReadPermission { get; init; }
    public bool HasWritePermission { get; init; }
    public bool IsSystemProtected { get; init; }
    public FolderValidationStatus ValidationStatus { get; init; } = FolderValidationStatus.Accessible;
    public WatcherHealthStatus WatcherHealth { get; init; } = WatcherHealthStatus.Healthy;
    public string? StatusDetail { get; init; }

    public IReadOnlyList<Guid> LinkedRuleIds { get; init; } = [];
    public IReadOnlyList<string> LinkedRuleNames { get; init; } = [];
    public int TotalRuleCount => LinkedRuleIds.Count;
    public int EnabledRuleCount { get; init; }
    public bool IsAnyRuleEnabled => EnabledRuleCount > 0;

    public bool IsTransientLocation => TransientLocations.IsTransient(NormalizedPath);

    public bool IncludeSubfolders { get; init; }
    public IReadOnlyList<string> ExclusionPatterns { get; init; } = [];
    public DateTime LastEvaluatedUtc { get; init; } = DateTime.UtcNow;

    public string AssociatedRulesSummary => LinkedRuleNames.Count > 0
        ? string.Join(", ", LinkedRuleNames)
        : "None";

    public string HealthSummary => ValidationStatus switch
    {
        FolderValidationStatus.Missing => "Directory does not exist",
        FolderValidationStatus.AccessDenied => "Access denied",
        FolderValidationStatus.InvalidPath => "Invalid path",
        _ => WatcherHealth switch
        {
            WatcherHealthStatus.Paused => "Monitoring paused",
            WatcherHealthStatus.Degraded => "Performance degraded",
            WatcherHealthStatus.Disconnected => "Folder disconnected",
            WatcherHealthStatus.Error => "Watcher error",
            _ => "Operational"
        }
    };

    public bool HasAlert => ValidationStatus != FolderValidationStatus.Accessible ||
                            WatcherHealth == WatcherHealthStatus.Degraded ||
                            WatcherHealth == WatcherHealthStatus.Disconnected ||
                            WatcherHealth == WatcherHealthStatus.Error;
}

