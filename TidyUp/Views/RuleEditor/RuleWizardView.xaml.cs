using System.Windows;
using TidyUp.ViewModels.RuleEditor;

namespace TidyUp.Views.RuleEditor;

/// <summary>
/// Interaction logic for RuleWizardView.xaml
/// </summary>
public partial class RuleWizardView : Window
{
    private bool _isClosingConfirmed;

    public RuleWizardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closing += OnClosing;
    }

    public RuleWizardView(RuleWizardViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is RuleWizardViewModel vm && vm.IsDirty && vm.CreatedRule == null && !_isClosingConfirmed)
        {
            var result = MessageBox.Show(
                "You have unsaved changes in the rule creation wizard. Discard unsaved changes?",
                "Discard Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _isClosingConfirmed = true;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RuleWizardViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is RuleWizardViewModel newVm)
        {
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose()
    {
        _isClosingConfirmed = true;

        if (DataContext is RuleWizardViewModel vm && vm.CreatedRule != null)
        {
            DialogResult = true;
        }
        else
        {
            DialogResult = false;
        }

        Close();
    }
}

