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

        var dialog = new SaveFileDialog
        {
            Title = "Export History to CSV",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = "csv",
            FileName = $"TidyUp_History_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            await vm.ExportToCsvAsync(dialog.FileName);
        }
    }

    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "Export History to JSON",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"TidyUp_History_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            await vm.ExportToJsonAsync(dialog.FileName);
        }
    }
}

