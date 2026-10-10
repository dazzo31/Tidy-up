using System.Windows;
using TidyUp.Models.Domain;
using TidyUp.ViewModels;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for PreviewWindow.xaml
/// </summary>
public partial class PreviewWindow : Window
{
    private readonly PreviewViewModel _viewModel;

    public PreviewWindow(PreviewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    /// <summary>
    /// Displays the window and runs a simulation for the specified rule.
    /// </summary>
    public async Task ShowPreviewAsync(Rule rule)
    {
        Show();
        await _viewModel.SimulateRuleAsync(rule);
    }

    /// <summary>
    /// Displays the window with an already generated ExecutionPlan.
    /// </summary>
    public void ShowPlan(ExecutionPlan plan)
    {
        _viewModel.LoadPlan(plan);
        Show();
    }

    private void ApproveAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAllApproved(true);
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAllApproved(false);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
