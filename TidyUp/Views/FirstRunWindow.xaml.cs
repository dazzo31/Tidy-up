using System.Windows;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for FirstRunWindow.xaml
/// </summary>
public partial class FirstRunWindow : Window
{
    public FirstRunWindow(ViewModels.FirstRunViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.OnFinishRequested = () => DialogResult = viewModel.WasCompleted;
    }
}
