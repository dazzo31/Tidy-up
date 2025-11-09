using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data;
using TidyUp.Data.Entities;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for the log viewer window.
/// </summary>
public partial class LogViewerViewModel : ObservableObject
{
    private readonly TidyUpDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<ActionLogEntity> _logs = new();

    [ObservableProperty]
    private ActionLogEntity? _selectedLog;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _filterStatus = "All";

    [ObservableProperty]
    private DateTime? _filterStartDate;

    [ObservableProperty]
    private DateTime? _filterEndDate;

    [ObservableProperty]
    private string? _filterRuleName;

    public ObservableCollection<string> StatusOptions { get; } = new()
    {
        "All",
        "Success",
        "Warning",
        "Error",
        "Skipped"
    };

    public LogViewerViewModel(TidyUpDbContext dbContext)
    {
        _dbContext = dbContext;
        
        // Set default date range to last 7 days
        FilterEndDate = DateTime.Now;
        FilterStartDate = DateTime.Now.AddDays(-7);
    }

    /// <summary>
    /// Loads logs from the database with current filters.
    /// </summary>
    [RelayCommand]
    private async Task LoadLogsAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading logs...";

        try
        {
            var query = _dbContext.ActionLogs.AsQueryable();

            // Apply date range filter
            if (FilterStartDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= FilterStartDate.Value);
            }

            if (FilterEndDate.HasValue)
            {
                var endDate = FilterEndDate.Value.Date.AddDays(1); // Include end date
                query = query.Where(l => l.Timestamp < endDate);
            }

            // Apply status filter
            if (FilterStatus != "All")
            {
                query = query.Where(l => l.Status == FilterStatus);
            }

            // Apply rule name filter
            if (!string.IsNullOrWhiteSpace(FilterRuleName))
            {
                query = query.Where(l => l.RuleName != null && l.RuleName.Contains(FilterRuleName));
            }

            // Apply search text filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(l => 
                    (l.FilePath != null && l.FilePath.Contains(SearchText)) ||
                    (l.ResultPath != null && l.ResultPath.Contains(SearchText)) ||
                    (l.ActionPerformed != null && l.ActionPerformed.Contains(SearchText)) ||
                    (l.ErrorMessage != null && l.ErrorMessage.Contains(SearchText)));
            }

            // Order by timestamp descending (newest first)
            query = query.OrderByDescending(l => l.Timestamp);

            var logs = await query.Take(1000).ToListAsync(); // Limit to 1000 records

            Logs.Clear();
            foreach (var log in logs)
            {
                Logs.Add(log);
            }

            StatusMessage = $"Loaded {logs.Count} log entries";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading logs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Clears all filters and reloads.
    /// </summary>
    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        FilterStatus = "All";
        FilterStartDate = DateTime.Now.AddDays(-7);
        FilterEndDate = DateTime.Now;
        FilterRuleName = null;

        await LoadLogsAsync();
    }

    /// <summary>
    /// Deletes logs older than the specified days.
    /// </summary>
    [RelayCommand]
    private async Task DeleteOldLogsAsync(int days)
    {
        IsLoading = true;
        StatusMessage = $"Deleting logs older than {days} days...";

        try
        {
            var cutoffDate = DateTime.Now.AddDays(-days);
            var oldLogs = await _dbContext.ActionLogs
                .Where(l => l.Timestamp < cutoffDate)
                .ToListAsync();

            _dbContext.ActionLogs.RemoveRange(oldLogs);
            await _dbContext.SaveChangesAsync();

            StatusMessage = $"Deleted {oldLogs.Count} old log entries";
            await LoadLogsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting logs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Exports logs to a CSV file.
    /// </summary>
    [RelayCommand]
    private async Task ExportLogsAsync(string filePath)
    {
        IsLoading = true;
        StatusMessage = "Exporting logs...";

        try
        {
            using var writer = new StreamWriter(filePath);
            
            // Write header
            await writer.WriteLineAsync("Timestamp,Rule Name,File Path,Action,Result Path,Status,Error Message");

            // Write data
            foreach (var log in Logs)
            {
                var line = string.Join(",",
                    EscapeCsv(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")),
                    EscapeCsv(log.RuleName ?? ""),
                    EscapeCsv(log.FilePath ?? ""),
                    EscapeCsv(log.ActionPerformed ?? ""),
                    EscapeCsv(log.ResultPath ?? ""),
                    EscapeCsv(log.Status ?? ""),
                    EscapeCsv(log.ErrorMessage ?? ""));
                
                await writer.WriteLineAsync(line);
            }

            StatusMessage = $"Exported {Logs.Count} logs to {System.IO.Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting logs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    partial void OnSearchTextChanged(string value)
    {
        // Debounce search - could use Reactive Extensions for better implementation
        _ = LoadLogsAsync();
    }

    partial void OnFilterStatusChanged(string value)
    {
        _ = LoadLogsAsync();
    }
}
