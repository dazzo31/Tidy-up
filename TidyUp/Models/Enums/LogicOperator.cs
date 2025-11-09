namespace TidyUp.Models.Enums;

/// <summary>
/// Defines the logical operator for combining multiple conditions.
/// </summary>
public enum LogicOperator
{
    /// <summary>
    /// All conditions must be true (AND logic).
    /// </summary>
    And,

    /// <summary>
    /// At least one condition must be true (OR logic).
    /// </summary>
    Or
}
