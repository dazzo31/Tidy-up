using System.Windows.Controls;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for LogsView.xaml
/// </summary>
public partial class LogsView : UserControl
{
    public LogsView()
    {
        InitializeComponent();
        
        // Load logs when view is loaded
        Loaded += async (s, e) =>
        {
            if (DataContext is ViewModels.LogsViewModel vm)
            {
                await vm.LoadLogsCommand.ExecuteAsync(null);
            }
        };
    }
}
