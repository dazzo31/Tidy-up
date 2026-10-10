using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data.Entities;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Validation;

namespace TidyUp.Data.Repositories;

/// <summary>
/// Repository for rule data operations with JSON serialization and validation.
/// </summary>
public class RuleRepository(TidyUpDbContext context, IRuleValidator? ruleValidator = null) : IRuleRepository
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<List<Rule>> GetAllAsync()
    {
        var entities = await context.Rules.ToListAsync();
        return entities.Select(EntityToModel).ToList();
    }

    public async Task<Rule?> GetByIdAsync(Guid id)
    {
        var entity = await context.Rules.FindAsync(id);
        return entity is null ? null : EntityToModel(entity);
    }

    public async Task<Rule> AddAsync(Rule rule)
    {
        if (ruleValidator is not null)
        {
            var validation = ruleValidator.ValidateRule(rule);
            if (!validation.IsValid)
                throw new RuleValidationException(validation.Errors);

            if (rule.IsEnabled)
            {
                var allRules = await GetAllAsync();
                allRules.Add(rule);
                var cycleValidation = ruleValidator.ValidateRules(allRules);
                if (!cycleValidation.IsValid)
                    throw new RuleValidationException(cycleValidation.Errors);
            }
        }

        var entity = ModelToEntity(rule);
        context.Rules.Add(entity);
        await context.SaveChangesAsync();
        return EntityToModel(entity);
    }

    public async Task UpdateAsync(Rule rule)
    {
        if (ruleValidator is not null)
        {
            var validation = ruleValidator.ValidateRule(rule);
            if (!validation.IsValid)
                throw new RuleValidationException(validation.Errors);

            if (rule.IsEnabled)
            {
                var allRules = await GetAllAsync();
                allRules.RemoveAll(r => r.Id == rule.Id);
                allRules.Add(rule);
                var cycleValidation = ruleValidator.ValidateRules(allRules);
                if (!cycleValidation.IsValid)
                    throw new RuleValidationException(cycleValidation.Errors);
            }
        }

        var existing = await context.Rules.FindAsync(rule.Id)
            ?? throw new InvalidOperationException($"Rule with ID {rule.Id} not found.");

        var entity = ModelToEntity(rule);
        entity.ModifiedDate = DateTime.UtcNow;

        // Update properties on the tracked entity instead of attaching a new one
        context.Entry(existing).CurrentValues.SetValues(entity);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await context.Rules.FindAsync(id);
        if (entity is not null)
        {
            context.Rules.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<Rule>> GetEnabledRulesOrderedAsync()
    {
        var entities = await context.Rules
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
            TriggerType = config.TriggerType,
            ScheduledTime = config.ScheduledTime,
            CreatedDate = entity.CreatedDate,
            ModifiedDate = entity.ModifiedDate,
            LastRunDate = entity.LastRunDate,
            FilesProcessedCount = entity.FilesProcessedCount
        };

        // Convert List to ObservableCollection
        if (config.MonitoredFolders is not null)
        {
            rule.MonitoredFolders = new ObservableCollection<MonitoredFolder>(config.MonitoredFolders);
        }

        if (config.Actions is not null)
        {
            rule.Actions = new ObservableCollection<FileAction>(config.Actions);
        }

        return rule;
    }

    private RuleEntity ModelToEntity(Rule rule)
    {
        var config = new RuleConfig
        {
            MonitoredFolders = new List<MonitoredFolder>(rule.MonitoredFolders),
            Conditions = rule.Conditions,
            Actions = new List<FileAction>(rule.Actions),
            TriggerType = rule.TriggerType,
            ScheduledTime = rule.ScheduledTime
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
        public RuleTriggerType TriggerType { get; set; } = RuleTriggerType.Continuous;
        public TimeSpan? ScheduledTime { get; set; }
    }
}
