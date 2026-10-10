using TidyUp.Models.Domain;

namespace TidyUp.Services.Validation;

/// <summary>
/// Static and runtime rule validator to detect recursive moves, source/destination folder nesting,
/// illegal filesystem paths, and cyclic cross-rule triggers.
/// </summary>
public interface IRuleValidator
{
    /// <summary>
    /// Validates an individual rule for path safety, self-referential nesting, and structural validity.
    /// </summary>
    RuleValidationResult ValidateRule(Rule rule);

    /// <summary>
    /// Validates an entire collection of rules, checking both individual rules and multi-rule cyclic loops.
    /// </summary>
    RuleValidationResult ValidateRules(IEnumerable<Rule> rules);
}

