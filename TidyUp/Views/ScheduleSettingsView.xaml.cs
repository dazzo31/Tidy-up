using System.Windows;
using TidyUp.ViewModels;

namespace TidyUp.Views;

public partial class ScheduleSettingsView : Window
{
    public ScheduleSettingsView(ScheduleSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

