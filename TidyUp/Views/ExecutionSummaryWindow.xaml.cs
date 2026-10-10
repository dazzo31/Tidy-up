using System.Windows;
using TidyUp.ViewModels;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for ExecutionSummaryWindow.xaml
/// </summary>
public partial class ExecutionSummaryWindow : Window
{
    public ExecutionSummaryViewModel ViewModel { get; }

    public ExecutionSummaryWindow(ExecutionSummaryViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        viewModel.RequestClose += () => Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

