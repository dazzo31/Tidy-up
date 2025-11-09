using TidyUp.Models.Domain;

namespace TidyUp.Data.Repositories;

/// <summary>
/// Repository interface for rule data operations.
/// </summary>
public interface IRuleRepository
{
    Task<List<Rule>> GetAllAsync();
    Task<Rule?> GetByIdAsync(Guid id);
    Task<Rule> AddAsync(Rule rule);
    Task UpdateAsync(Rule rule);
    Task DeleteAsync(Guid id);
    Task<List<Rule>> GetEnabledRulesOrderedAsync();
}
