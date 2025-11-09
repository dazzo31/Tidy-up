using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TidyUp.Data.Entities;
using TidyUp.Data.Repositories;

namespace TidyUp.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly IActionLogRepository _logRepository;
    private List<ActionLogEntity> _allLogs = new();

    [ObservableProperty]
    private ObservableCollection<ActionLogEntity> _logs = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableStatuses = new() { "All", "Success", "Warning", "Error" };

    [ObservableProperty]
    private ObservableCollection<string> _availableRules = new() { "All Rules" };

    [ObservableProperty]
    private string _selectedStatus = "All";

    [ObservableProperty]
    private string _selectedRule = "All Rules";

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private int _totalCount = 0;

    [ObservableProperty]
    private int _successCount = 0;

    [ObservableProperty]
    private int _errorCount = 0;

    [ObservableProperty]
    private int _warningCount = 0;

    public LogsViewModel(IActionLogRepository logRepository)
    {
        _logRepository = logRepository;
    }

    [RelayCommand]
    private async Task LoadLogsAsync()
    {
        try
        {
            StatusMessage = "Loading logs...";
            
            // Load all logs
            _allLogs = await _logRepository.GetAllAsync();
            
            // Update available rules dropdown
            AvailableRules.Clear();
            AvailableRules.Add("All Rules");
            var ruleNames = _allLogs.Select(l => l.RuleName).Distinct().OrderBy(n => n);
            foreach (var ruleName in ruleNames)
            {
                AvailableRules.Add(ruleName);
            }
            
            // Calculate statistics
            TotalCount = _allLogs.Count;
            SuccessCount = _allLogs.Count(l => l.Status == "Success");
            ErrorCount = _allLogs.Count(l => l.Status == "Error");
            WarningCount = _allLogs.Count(l => l.Status == "Warning");
            
            // Apply filters
            ApplyFilters();
            
            StatusMessage = $"Loaded {TotalCount} log entries";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading logs: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        var filtered = _allLogs.AsEnumerable();

        // Filter by status
        if (SelectedStatus != "All")
        {
            filtered = filtered.Where(l => l.Status == SelectedStatus);
        }

        // Filter by rule
        if (SelectedRule != "All Rules")
        {
            filtered = filtered.Where(l => l.RuleName == SelectedRule);
        }

        // Filter by date range
        filtered = filtered.Where(l => l.Timestamp >= StartDate && l.Timestamp <= EndDate);

        // Filter by search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLower();
            filtered = filtered.Where(l =>
                l.RuleName.ToLower().Contains(search) ||
                l.FilePath.ToLower().Contains(search) ||
                l.ActionPerformed.ToLower().Contains(search) ||
                (l.ErrorMessage != null && l.ErrorMessage.ToLower().Contains(search)));
        }

        // Update UI
        Logs.Clear();
        foreach (var log in filtered.OrderByDescending(l => l.Timestamp))
        {
            Logs.Add(log);
        }

        StatusMessage = $"Showing {Logs.Count} of {TotalCount} log entries";
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedStatus = "All";
        SelectedRule = "All Rules";
        StartDate = DateTime.Today.AddDays(-7);
        EndDate = DateTime.Today.AddDays(1);
        SearchText = "";
        ApplyFilters();
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"tidyup-logs-{DateTime.Now:yyyy-MM-dd}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusMessage = "Exporting to CSV...";

                var csv = new StringBuilder();
                csv.AppendLine("Timestamp,Rule Name,File Path,Action,Result Path,Status,Error Message");

                foreach (var log in Logs)
                {
                    csv.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\"," +
                                  $"\"{EscapeCsv(log.RuleName)}\"," +
                                  $"\"{EscapeCsv(log.FilePath)}\"," +
                                  $"\"{EscapeCsv(log.ActionPerformed)}\"," +
                                  $"\"{EscapeCsv(log.ResultPath ?? "")}\"," +
                                  $"\"{log.Status}\"," +
                                  $"\"{EscapeCsv(log.ErrorMessage ?? "")}\"");
                }

                await File.WriteAllTextAsync(dialog.FileName, csv.ToString());
                StatusMessage = $"Exported {Logs.Count} entries to {Path.GetFileName(dialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteOldLogsAsync()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30); // Using hardcoded 30 days for now
            StatusMessage = "Deleting old logs...";
            
            await _logRepository.DeleteOlderThanAsync(cutoffDate);
            await LoadLogsAsync();
            
            StatusMessage = "Old logs deleted successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting logs: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearAllLogsAsync()
    {
        try
        {
            // TODO: Add confirmation dialog
            StatusMessage = "Clearing all logs...";
            
            await _logRepository.DeleteAllAsync();
            await LoadLogsAsync();
            
            StatusMessage = "All logs cleared";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing logs: {ex.Message}";
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Replace("\"", "\"\"");
    }

    partial void OnSelectedStatusChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedRuleChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }
}
