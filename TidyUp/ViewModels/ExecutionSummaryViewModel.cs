using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Services.Diagnostics;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for displaying post-execution summary and discrepancy audit reports.
/// Highlights skipped locks, permission failures, missing files, or post-simulation modifications.
/// </summary>
public partial class ExecutionSummaryViewModel : ObservableObject
{
    [ObservableProperty]
    private ExecutionDiscrepancyReport? _report;

    [ObservableProperty]
    private bool _showDiscrepanciesOnly;

    public ObservableCollection<ActionDiscrepancyItem> DisplayItems { get; } = new();

    public bool HasReport => Report != null;
    public bool HasDiscrepancies => Report?.HasAnyDiscrepancies ?? false;
    public string SummaryText => Report?.SummaryText ?? "No execution data available.";
    public int TotalPlanned => Report?.TotalPlanned ?? 0;
    public int ExecutedAsPlannedCount => Report?.ExecutedAsPlannedCount ?? 0;
    public int DiscrepancyCount => Report?.DiscrepancyCount ?? 0;

    public event Action? RequestClose;

    public ExecutionSummaryViewModel(ExecutionDiscrepancyReport? report = null)
    {
        if (report != null)
        {
            LoadReport(report);
        }
    }

    public void LoadReport(ExecutionDiscrepancyReport report)
    {
        Report = report;
        OnPropertyChanged(nameof(HasReport));
        OnPropertyChanged(nameof(HasDiscrepancies));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(TotalPlanned));
        OnPropertyChanged(nameof(ExecutedAsPlannedCount));
        OnPropertyChanged(nameof(DiscrepancyCount));

        // If there are discrepancies, default to showing discrepancies only to highlight issues
        if (report.HasAnyDiscrepancies)
        {
            ShowDiscrepanciesOnly = true;
        }

        RefreshDisplayItems();
    }

    partial void OnShowDiscrepanciesOnlyChanged(bool value)
    {
        RefreshDisplayItems();
    }

    private void RefreshDisplayItems()
    {
        DisplayItems.Clear();
        if (Report == null)
            return;

        var items = ShowDiscrepanciesOnly
            ? Report.Items.Where(i => i.HasDiscrepancy)
            : Report.Items;

        foreach (var item in items)
        {
            DisplayItems.Add(item);
        }
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }
}
