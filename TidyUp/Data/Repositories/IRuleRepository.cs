using TidyUp.Models.Domain;

namespace TidyUp.Data.Repositories;

/// <summary>
/// Event arguments for rule repository change notifications.
/// </summary>
public class RulesChangedEventArgs : EventArgs
{
    public Guid? AffectedRuleId { get; init; }
    public string ChangeType { get; init; } = string.Empty;
}

/// <summary>
/// Repository interface for rule data operations.
/// </summary>
public interface IRuleRepository
{
    event EventHandler<RulesChangedEventArgs>? RulesChanged;

    Task<List<Rule>> GetAllAsync();
    Task<Rule?> GetByIdAsync(Guid id);
    Task<Rule> AddAsync(Rule rule);
    Task<List<Rule>> AddRangeAsync(IEnumerable<Rule> rules);
    Task UpdateAsync(Rule rule);
    Task DeleteAsync(Guid id);
    Task<List<Rule>> GetEnabledRulesOrderedAsync();
    Task UpdateExecutionOrdersAsync(IEnumerable<(Guid RuleId, int Order)> orderings);
}

