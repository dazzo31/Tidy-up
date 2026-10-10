namespace TidyUp.Services.Validation;

/// <summary>
/// Error code identifying specific rule configuration defects.
/// </summary>
public enum RuleValidationErrorCode
{
    None = 0,
    EmptyRuleName,
    MissingMonitoredFolders,
    MissingActions,
    EmptyPath,
    InvalidPathCharacters,
    DirectoryTraversal,
    ReservedDeviceName,
    PathTooLong,
    IdenticalSourceAndDestination,
    DestinationIsSubfolderOfMonitoredSource,
    CrossRuleCycleDetected
}

/// <summary>
/// Detailed validation error entry.
/// </summary>
public class RuleValidationError
{
    public string RuleName { get; init; } = string.Empty;
    public Guid? RuleId { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public RuleValidationErrorCode ErrorCode { get; init; } = RuleValidationErrorCode.None;

    public override string ToString() => $"[{ErrorCode}] {RuleName}.{PropertyName}: {ErrorMessage}";
}

/// <summary>
/// Detailed validation warning entry.
/// </summary>
public class RuleValidationWarning
{
    public string RuleName { get; init; } = string.Empty;
    public Guid? RuleId { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string WarningMessage { get; init; } = string.Empty;

    public override string ToString() => $"{RuleName}.{PropertyName}: {WarningMessage}";
}

/// <summary>
/// Outcome of static or cross-rule validation.
/// </summary>
public class RuleValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public bool HasWarnings => Warnings.Count > 0;
    public List<RuleValidationError> Errors { get; init; } = new();
    public List<RuleValidationWarning> Warnings { get; init; } = new();

    public static RuleValidationResult Success() => new();

    public static RuleValidationResult Failure(RuleValidationError error)
    {
        var result = new RuleValidationResult();
        result.Errors.Add(error);
        return result;
    }
}

/// <summary>
/// Exception thrown when attempting to persist or enable an invalid or cyclic rule.
/// </summary>
public class RuleValidationException : Exception
{
    public IReadOnlyList<RuleValidationError> ValidationErrors { get; }

    public RuleValidationException(string message, IEnumerable<RuleValidationError> errors)
        : base(message)
    {
        ValidationErrors = errors.ToList();
    }

    public RuleValidationException(IEnumerable<RuleValidationError> errors)
        : this("Rule validation failed. See ValidationErrors for details.", errors)
    {
    }
}

