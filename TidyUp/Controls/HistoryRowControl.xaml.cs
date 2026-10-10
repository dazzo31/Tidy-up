using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TidyUp.ViewModels;

namespace TidyUp.Controls;

/// <summary>
/// Interaction logic for HistoryRowControl.xaml
/// </summary>
public partial class HistoryRowControl : UserControl
{
    public HistoryRowControl()
    {
        InitializeComponent();
    }

    private void UndoOperation_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HistoryRowItemViewModel rowVm)
        {
            var historyVm = FindParentHistoryViewModel();
            if (historyVm != null)
            {
                _ = historyVm.RollbackOperationCommand.ExecuteAsync(rowVm);
            }
        }
    }

    private void UndoBatch_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HistoryRowItemViewModel rowVm)
        {
            var historyVm = FindParentHistoryViewModel();
            if (historyVm != null)
            {
                _ = historyVm.RollbackBatchCommand.ExecuteAsync(rowVm);
            }
        }
    }

    private HistoryViewModel? FindParentHistoryViewModel()
    {
        DependencyObject? current = this;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is HistoryViewModel vm)
            {
                return vm;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

