using System.Windows;
using TidyUp.Models.Domain;
using TidyUp.ViewModels;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for RulePreviewWindow.xaml
/// </summary>
public partial class RulePreviewWindow : Window
{
    private readonly RulePreviewViewModel _viewModel;

    public RulePreviewWindow(RulePreviewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    /// <summary>
    /// Sets the rule to preview and runs the preview automatically.
    /// </summary>
    public async Task ShowPreviewAsync(Rule rule)
    {
        _viewModel.SetRule(rule);
        Show();
        await _viewModel.RunPreviewCommand.ExecuteAsync(null);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
