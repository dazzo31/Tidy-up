using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TidyUp.ViewModels;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for HistoryView.xaml
/// </summary>
public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        Loaded += HistoryView_Loaded;
    }

    private async void HistoryView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is HistoryViewModel vm && vm.HistoryEntries.Count == 0)
        {
            await vm.LoadPageAsync();
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm)
            return;

        await PromptAndExportCsvAsync(vm, filteredOnly: vm.IsFilterActive);
    }

    private async void ExportFilteredCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm)
            return;

        await PromptAndExportCsvAsync(vm, filteredOnly: true);
    }

    private async void ExportAllCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm)
            return;

        await PromptAndExportCsvAsync(vm, filteredOnly: false);
    }

    private static async Task PromptAndExportCsvAsync(HistoryViewModel vm, bool filteredOnly)
    {
        var dialog = new SaveFileDialog
        {
            Title = filteredOnly ? "Export Filtered History to CSV" : "Export All History to CSV",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = "csv",
            FileName = $"TidyUp_History_{(filteredOnly ? "Filtered_" : "All_")}{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            await vm.ExportToCsvAsync(dialog.FileName, filteredOnly);
        }
    }

    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm)
            return;

        await PromptAndExportJsonAsync(vm, filteredOnly: vm.IsFilterActive);
    }

    private async void ExportFilteredJson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm)
            return;

        await PromptAndExportJsonAsync(vm, filteredOnly: true);
    }

    private async void ExportAllJson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm)
            return;

        await PromptAndExportJsonAsync(vm, filteredOnly: false);
    }

    private static async Task PromptAndExportJsonAsync(HistoryViewModel vm, bool filteredOnly)
    {
        var dialog = new SaveFileDialog
        {
            Title = filteredOnly ? "Export Filtered History to JSON" : "Export All History to JSON",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"TidyUp_History_{(filteredOnly ? "Filtered_" : "All_")}{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            await vm.ExportToJsonAsync(dialog.FileName, filteredOnly);
        }
    }
}

