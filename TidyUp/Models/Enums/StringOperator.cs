namespace TidyUp.Models.Enums;

/// <summary>
/// Defines comparison operators for string-based conditions.
/// </summary>
public enum StringOperator
{
    /// <summary>
    /// Exact match (case-insensitive).
    /// </summary>
    Is,

    /// <summary>
    /// Not an exact match (case-insensitive).
    /// </summary>
    IsNot,

    /// <summary>
    /// Contains the specified substring (case-insensitive).
    /// </summary>
    Contains,

    /// <summary>
    /// Does not contain the specified substring (case-insensitive).
    /// </summary>
    DoesNotContain,

    /// <summary>
    /// Starts with the specified prefix (case-insensitive).
    /// </summary>
    StartsWith,

    /// <summary>
    /// Ends with the specified suffix (case-insensitive).
    /// </summary>
    EndsWith,

    /// <summary>
    /// Matches the regular expression pattern.
    /// </summary>
    MatchesRegex,

    /// <summary>
    /// Is empty or null.
    /// </summary>
    IsEmpty
}
