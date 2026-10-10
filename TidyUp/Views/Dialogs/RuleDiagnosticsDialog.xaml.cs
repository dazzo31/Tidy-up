using System.Windows;
using Microsoft.Win32;
using TidyUp.ViewModels;

namespace TidyUp.Views.Dialogs;

/// <summary>
/// Interaction logic for RuleDiagnosticsDialog.xaml
/// </summary>
public partial class RuleDiagnosticsDialog : Window
{
    public RuleEvaluationInspectorViewModel ViewModel { get; }

    public RuleDiagnosticsDialog(RuleEvaluationInspectorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        Loaded += async (s, e) =>
        {
            await ViewModel.LoadRulesAsync();
        };
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select File to Inspect",
            Filter = "All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.TargetFilePath = dialog.FileName;
            if (ViewModel.CanInspect)
            {
                ViewModel.InspectFileCommand.Execute(null);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

