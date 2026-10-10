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
    }

    public RuleWizardView(RuleWizardViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
