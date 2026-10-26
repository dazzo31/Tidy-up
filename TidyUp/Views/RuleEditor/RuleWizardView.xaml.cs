using System.Windows;
using System.Windows.Controls;
using TidyUp.ViewModels.RuleEditor;

namespace TidyUp.Views.RuleEditor;

/// <summary>
/// Interaction logic for RuleWizardView.xaml (UserControl embeddable within MainWindow)
/// </summary>
public partial class RuleWizardView : UserControl
{
    public RuleWizardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public RuleWizardView(RuleWizardViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RuleWizardViewModel oldVm)
        {
            oldVm.RequestClose -= HandleRequestClose;
        }
        if (e.NewValue is RuleWizardViewModel newVm)
        {
            newVm.RequestClose += HandleRequestClose;
        }
    }

    private void HandleRequestClose()
    {
        var window = Window.GetWindow(this);
        if (window != null && window != Application.Current?.MainWindow)
        {
            window.Close();
        }
    }
}
