using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data.Entities;
using TidyUp.Models.Domain;

namespace TidyUp.Data.Repositories;

/// <summary>
/// Repository for rule data operations with JSON serialization.
/// </summary>
public class RuleRepository : IRuleRepository
{
    private readonly TidyUpDbContext _context;
    private readonly JsonSerializerOptions _jsonOptions;

    public RuleRepository(TidyUpDbContext context)
    {
        _context = context;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<List<Rule>> GetAllAsync()
    {
        var entities = await _context.Rules.ToListAsync();
        return entities.Select(EntityToModel).ToList();
    }

    public async Task<Rule?> GetByIdAsync(Guid id)
    {
        var entity = await _context.Rules.FindAsync(id);
        return entity == null ? null : EntityToModel(entity);
    }

    public async Task<Rule> AddAsync(Rule rule)
    {
        var entity = ModelToEntity(rule);
        _context.Rules.Add(entity);
        await _context.SaveChangesAsync();
        return EntityToModel(entity);
    }

    public async Task UpdateAsync(Rule rule)
    {
        var entity = ModelToEntity(rule);
        entity.ModifiedDate = DateTime.UtcNow;
        _context.Rules.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.Rules.FindAsync(id);
        if (entity != null)
        {
            _context.Rules.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Rule>> GetEnabledRulesOrderedAsync()
    {
        var entities = await _context.Rules
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.ExecutionOrder)
            .ToListAsync();
        
        return entities.Select(EntityToModel).ToList();
    }

    private Rule EntityToModel(RuleEntity entity)
    {
        var config = JsonSerializer.Deserialize<RuleConfig>(entity.ConfigJson, _jsonOptions) 
            ?? new RuleConfig();

        var rule = new Rule
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Conditions = config.Conditions,
            IsEnabled = entity.IsEnabled,
            ExecutionOrder = entity.ExecutionOrder,
            StopProcessingAfterMatch = entity.StopProcessingAfterMatch,
            CreatedDate = entity.CreatedDate,
            ModifiedDate = entity.ModifiedDate,
            LastRunDate = entity.LastRunDate,
            FilesProcessedCount = entity.FilesProcessedCount
        };
        
        // Convert List to ObservableCollection
        if (config.MonitoredFolders != null)
        {
            foreach (var folder in config.MonitoredFolders)
            {
                rule.MonitoredFolders.Add(folder);
            }
        }
        
        if (config.Actions != null)
        {
            foreach (var action in config.Actions)
            {
                rule.Actions.Add(action);
            }
        }
        
        return rule;
    }

    private RuleEntity ModelToEntity(Rule rule)
    {
        var config = new RuleConfig
        {
            MonitoredFolders = rule.MonitoredFolders.ToList(),
            Conditions = rule.Conditions,
            Actions = rule.Actions.ToList()
        };

        return new RuleEntity
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            ConfigJson = JsonSerializer.Serialize(config, _jsonOptions),
            IsEnabled = rule.IsEnabled,
            ExecutionOrder = rule.ExecutionOrder,
            StopProcessingAfterMatch = rule.StopProcessingAfterMatch,
            CreatedDate = rule.CreatedDate,
            ModifiedDate = rule.ModifiedDate,
            LastRunDate = rule.LastRunDate,
            FilesProcessedCount = rule.FilesProcessedCount
        };
    }

    /// <summary>
    /// Helper class for JSON serialization of rule configuration.
    /// </summary>
    private class RuleConfig
    {
        public List<MonitoredFolder>? MonitoredFolders { get; set; }
        public ConditionGroup? Conditions { get; set; }
        public List<FileAction>? Actions { get; set; }
    }
}
