using TidyUp.Models.Enums;
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

    /// <summary>The single status every screen shows for this folder.</summary>
    public FolderStatus Status => FolderStatusResolver.Resolve(ValidationStatus, WatcherHealth, IsSystemProtected);

    public string HealthSummary => Status switch
    {
        FolderStatus.Missing => "Directory does not exist",
        FolderStatus.AccessDenied => "Access denied",
        FolderStatus.Protected => "Protected system path",
        FolderStatus.Paused => "Monitoring paused",
        FolderStatus.Degraded => "Performance degraded",
        FolderStatus.Error => ValidationStatus == FolderValidationStatus.InvalidPath ? "Invalid path" : "Watcher error",
        _ => "Operational"
    };

    public bool HasAlert => ValidationStatus != FolderValidationStatus.Accessible ||
                            WatcherHealth == WatcherHealthStatus.Degraded ||
                            WatcherHealth == WatcherHealthStatus.Disconnected ||
                            WatcherHealth == WatcherHealthStatus.Error;
}

