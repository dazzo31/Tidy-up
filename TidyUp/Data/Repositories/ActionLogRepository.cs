using Microsoft.EntityFrameworkCore;
using TidyUp.Data.Entities;

namespace TidyUp.Data.Repositories;

public interface IActionLogRepository
{
    Task<List<ActionLogEntity>> GetAllAsync();
    Task<List<ActionLogEntity>> GetByRuleIdAsync(Guid ruleId);
    Task<List<ActionLogEntity>> GetByStatusAsync(string status);
    Task<List<ActionLogEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<List<ActionLogEntity>> SearchAsync(string searchText);
    Task AddAsync(ActionLogEntity log);
    Task DeleteOlderThanAsync(DateTime cutoffDate);
    Task DeleteAllAsync();
}

public class ActionLogRepository : IActionLogRepository
{
    private readonly TidyUpDbContext _context;

    public ActionLogRepository(TidyUpDbContext context)
    {
        _context = context;
    }

    public async Task<List<ActionLogEntity>> GetAllAsync()
    {
        return await _context.ActionLogs
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<ActionLogEntity>> GetByRuleIdAsync(Guid ruleId)
    {
        return await _context.ActionLogs
            .Where(l => l.RuleId == ruleId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<ActionLogEntity>> GetByStatusAsync(string status)
    {
        return await _context.ActionLogs
            .Where(l => l.Status == status)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<ActionLogEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.ActionLogs
            .Where(l => l.Timestamp >= startDate && l.Timestamp <= endDate)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<ActionLogEntity>> SearchAsync(string searchText)
    {
        var search = searchText.ToLower();
        return await _context.ActionLogs
            .Where(l => 
                l.RuleName.ToLower().Contains(search) ||
                l.FilePath.ToLower().Contains(search) ||
                l.ActionPerformed.ToLower().Contains(search) ||
                (l.ErrorMessage != null && l.ErrorMessage.ToLower().Contains(search)))
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task AddAsync(ActionLogEntity log)
    {
        _context.ActionLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteOlderThanAsync(DateTime cutoffDate)
    {
        var oldLogs = await _context.ActionLogs
            .Where(l => l.Timestamp < cutoffDate)
            .ToListAsync();

        _context.ActionLogs.RemoveRange(oldLogs);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAllAsync()
    {
        _context.ActionLogs.RemoveRange(_context.ActionLogs);
        await _context.SaveChangesAsync();
    }
}
