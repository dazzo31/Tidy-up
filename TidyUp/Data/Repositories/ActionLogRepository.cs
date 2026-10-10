using Microsoft.EntityFrameworkCore;
using TidyUp.Data.Entities;

namespace TidyUp.Data.Repositories;

public interface IActionLogRepository
{
    Task<List<ActionLogEntity>> GetFilteredAsync(
        string? status = null,
        string? ruleName = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? searchText = null,
        int maxResults = 1000);
    Task<LogStatistics> GetStatisticsAsync();
    Task<List<string>> GetDistinctRuleNamesAsync();
    Task<List<ActionLogEntity>> GetByRuleIdAsync(Guid ruleId);
    Task<List<ActionLogEntity>> GetByStatusAsync(string status);
    Task<List<ActionLogEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<List<ActionLogEntity>> SearchAsync(string searchText);
    Task AddAsync(ActionLogEntity log);
    Task DeleteOlderThanAsync(DateTime cutoffDate);
    Task DeleteAllAsync();
}

public class LogStatistics
{
    public int Total { get; set; }
    public int Success { get; set; }
    public int Warning { get; set; }
    public int Error { get; set; }
}

public class ActionLogRepository(TidyUpDbContext context) : IActionLogRepository
{
    public async Task<List<ActionLogEntity>> GetFilteredAsync(
        string? status = null,
        string? ruleName = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? searchText = null,
        int maxResults = 1000)
    {
        var query = context.ActionLogs.AsQueryable();

        if (!string.IsNullOrEmpty(status) && status != "All")
            query = query.Where(l => l.Status == status);

        if (!string.IsNullOrEmpty(ruleName) && ruleName != "All Rules")
            query = query.Where(l => l.RuleName == ruleName);

        if (startDate.HasValue)
            query = query.Where(l => l.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(l => l.Timestamp <= endDate.Value);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
                var pattern = $"%{searchText}%";
            query = query.Where(l =>
                EF.Functions.Like(l.RuleName, pattern) ||
                EF.Functions.Like(l.FilePath, pattern) ||
                EF.Functions.Like(l.ActionPerformed, pattern) ||
                (l.ErrorMessage != null && EF.Functions.Like(l.ErrorMessage, pattern)));
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<LogStatistics> GetStatisticsAsync()
    {
        return new LogStatistics
        {
            Total = await context.ActionLogs.CountAsync(),
            Success = await context.ActionLogs.CountAsync(l => l.Status == "Success"),
            Warning = await context.ActionLogs.CountAsync(l => l.Status == "Warning"),
            Error = await context.ActionLogs.CountAsync(l => l.Status == "Error")
        };
    }

    public async Task<List<string>> GetDistinctRuleNamesAsync()
    {
        return await context.ActionLogs
            .Select(l => l.RuleName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
    }

    public async Task<List<ActionLogEntity>> GetByRuleIdAsync(Guid ruleId)
    {
        return await context.ActionLogs
            .Where(l => l.RuleId == ruleId)
            .OrderByDescending(l => l.Timestamp)
            .Take(1000)
            .ToListAsync();
    }

    public async Task<List<ActionLogEntity>> GetByStatusAsync(string status)
    {
        return await context.ActionLogs
            .Where(l => l.Status == status)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<ActionLogEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await context.ActionLogs
            .Where(l => l.Timestamp >= startDate && l.Timestamp <= endDate)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<ActionLogEntity>> SearchAsync(string searchText)
    {
        var pattern = $"%{searchText}%";
        return await context.ActionLogs
            .Where(l =>
                EF.Functions.Like(l.RuleName, pattern) ||
                EF.Functions.Like(l.FilePath, pattern) ||
                EF.Functions.Like(l.ActionPerformed, pattern) ||
                (l.ErrorMessage != null && EF.Functions.Like(l.ErrorMessage, pattern)))
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task AddAsync(ActionLogEntity log)
    {
        context.ActionLogs.Add(log);
        await context.SaveChangesAsync();
    }

    public async Task DeleteOlderThanAsync(DateTime cutoffDate)
    {
        await context.ActionLogs
            .Where(l => l.Timestamp < cutoffDate)
            .ExecuteDeleteAsync();
    }

    public async Task DeleteAllAsync()
    {
        await context.ActionLogs.ExecuteDeleteAsync();
    }
}
