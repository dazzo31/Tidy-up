using System.Windows;
using Microsoft.Win32;
using TidyUp.ViewModels;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for LogViewerWindow.xaml
/// </summary>
public partial class LogViewerWindow : Window
{
    private readonly LogViewerViewModel _viewModel;

    public LogViewerWindow(LogViewerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Load logs when window is loaded
        Loaded += async (s, e) => await _viewModel.LoadLogsCommand.ExecuteAsync(null);
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = "csv",
            FileName = $"action-logs-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.ExportLogsCommand.ExecuteAsync(dialog.FileName);
        }
    }

    private async void DeleteOldLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Delete logs older than 30 days?\n\nThis cannot be undone.",
            "Delete Old Logs",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await _viewModel.DeleteOldLogsCommand.ExecuteAsync(30);
        }
    }
}
