using System.Windows;
using TidyUp.ViewModels.Dialogs;

namespace TidyUp.Views.Dialogs;

/// <summary>
/// Interaction logic for SafeguardConfirmationDialog.xaml
/// </summary>
public partial class SafeguardConfirmationDialog : Window
{
    public SafeguardConfirmationViewModel ViewModel { get; }

    public SafeguardConfirmationDialog(SafeguardConfirmationViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;

        ViewModel.RequestClose = () =>
        {
            DialogResult = ViewModel.IsConfirmed;
            Close();
        };
    }
}

