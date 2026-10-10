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

    [ObservableProperty]
    private ObservableCollection<ActionLogEntity> _logs = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableStatuses = ["All", "Success", "Warning", "Error"];

    [ObservableProperty]
    private ObservableCollection<string> _availableRules = ["All Rules"];

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
    private int _totalCount;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

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

            // Load statistics via efficient COUNT queries
            var stats = await _logRepository.GetStatisticsAsync();
            TotalCount = stats.Total;
            SuccessCount = stats.Success;
            ErrorCount = stats.Error;
            WarningCount = stats.Warning;

            // Update available rules dropdown
            AvailableRules.Clear();
            AvailableRules.Add("All Rules");
            var ruleNames = await _logRepository.GetDistinctRuleNamesAsync();
            foreach (var ruleName in ruleNames)
            {
                AvailableRules.Add(ruleName);
            }

            // Load filtered results from database
            await ApplyFiltersAsync();

            StatusMessage = $"Loaded {TotalCount} log entries";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading logs: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        try
        {
            var filtered = await _logRepository.GetFilteredAsync(
                status: SelectedStatus,
                ruleName: SelectedRule,
                startDate: StartDate,
                endDate: EndDate,
                searchText: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);

            Logs.Clear();
            foreach (var log in filtered)
            {
                Logs.Add(log);
            }

            StatusMessage = $"Showing {Logs.Count} of {TotalCount} log entries";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying filters: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SelectedStatus = "All";
        SelectedRule = "All Rules";
        StartDate = DateTime.Today.AddDays(-7);
        EndDate = DateTime.Today.AddDays(1);
        SearchText = "";
        await ApplyFiltersAsync();
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
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
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
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to delete ALL log entries?\n\nThis action cannot be undone.",
                "Clear All Logs",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = "Clear cancelled";
                return;
            }

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
        _ = ApplyFiltersAsync();
    }

    partial void OnSelectedRuleChanged(string value)
    {
        _ = ApplyFiltersAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = ApplyFiltersAsync();
    }
}
