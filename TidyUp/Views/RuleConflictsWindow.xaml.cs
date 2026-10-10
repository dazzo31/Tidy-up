using System.Windows;
using TidyUp.ViewModels;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for RuleConflictsWindow.xaml
/// </summary>
public partial class RuleConflictsWindow : Window
{
    public RuleConflictsWindow(RuleConflictsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

