using System.Windows;
using TidyUp.ViewModels;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for RuleRevisionDiffView.xaml
/// </summary>
public partial class RuleRevisionDiffView : Window
{
    public RuleRevisionDiffView(RuleRevisionDiffViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (s, e) => await viewModel.LoadRevisionsCommand.ExecuteAsync(null);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

