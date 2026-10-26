namespace TidyUp.Models.Domain;

/// <summary>
/// Scoped execution authorization context issued by confirmation or policy boundary
/// granting explicit permission for destructive file effects (e.g. permanent deletion, overwrite).
/// Decouples execution authorization from persisted rule preferences.
/// </summary>
public class ExecutionAuthorizationContext
{
    public Guid? PlanId { get; init; }
    public Guid? PlanRevisionId { get; init; }
    public Guid? RuleId { get; init; }
    public HashSet<Guid> AuthorizedActionIds { get; init; } = new();
    public HashSet<string> AuthorizedPermanentDeletionPaths { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AuthorizedOverwritePaths { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public bool AuthorizePermanentDeletion { get; set; } = true;

    public bool IsActionAuthorized(Guid actionId) => AuthorizedActionIds.Contains(actionId);

    public bool IsActionAuthorized(Guid actionId, string path)
    {
        if (!IsActionAuthorized(actionId))
            return false;

        return IsPermanentDeleteAuthorized(path);
    }

    public bool IsPermanentDeleteAuthorized(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return AuthorizedPermanentDeletionPaths.Contains(path);
    }

    public bool IsOverwriteAuthorized(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return AuthorizedOverwritePaths.Contains(path);
    }
}
