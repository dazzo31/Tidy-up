using System.Windows;

namespace TidyUp.Views;

/// <summary>
/// Interaction logic for FirstRunWindow.xaml
/// </summary>
public partial class FirstRunWindow : Window
{
    public FirstRunWindow()
    {
        InitializeComponent();
        
        // Subscribe to finish event
        if (DataContext is ViewModels.FirstRunViewModel vm)
        {
            vm.OnFinishRequested = () =>
            {
                DialogResult = true;
                Close();
            };
        }
    }
}
