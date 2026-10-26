namespace TidyUp.Models.Domain;

/// <summary>
/// Scoped authorization context binding user consent to a specific plan execution,
/// approved action IDs, and concrete destructive effects.
/// Replaces unconstrained boolean authorization flags to prevent consent hijacking.
/// </summary>
public record ExecutionAuthorizationContext
{
    /// <summary>Unique identifier of the execution plan for which authorization was granted.</summary>
    public Guid PlanId { get; init; }

    /// <summary>Rule ID governing this execution.</summary>
    public Guid RuleId { get; init; }

    /// <summary>Specific action IDs explicitly approved by the user.</summary>
    public HashSet<Guid> AuthorizedActionIds { get; init; } = new();

    /// <summary>Specific file paths explicitly authorized for permanent deletion.</summary>
    public HashSet<string> AuthorizedPermanentDeletionPaths { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Specific file paths explicitly authorized for overwrite.</summary>
    public HashSet<string> AuthorizedOverwritePaths { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>UTC timestamp when authorization was granted.</summary>
    public DateTime IssuedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Checks whether execution is authorized for a specific action ID.
    /// </summary>
    public bool IsActionAuthorized(Guid actionId) => AuthorizedActionIds.Contains(actionId);

    /// <summary>
    /// Checks whether permanent deletion is authorized for a specific file path.
    /// </summary>
    public bool IsPermanentDeleteAuthorized(string path) =>
        !string.IsNullOrWhiteSpace(path) && AuthorizedPermanentDeletionPaths.Contains(path);

    /// <summary>
    /// Checks whether overwrite is authorized for a specific file path.
    /// </summary>
    public bool IsOverwriteAuthorized(string path) =>
        !string.IsNullOrWhiteSpace(path) && AuthorizedOverwritePaths.Contains(path);
}
