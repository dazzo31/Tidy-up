using System.Windows;
using System.Windows.Controls;
using TidyUp.ViewModels;

namespace TidyUp.Controls;

/// <summary>
/// Interaction logic for ConditionEditorControl.xaml
/// </summary>
public partial class ConditionEditorControl : UserControl
{
    public ConditionEditorControl()
    {
        InitializeComponent();
        
        // Set default DataContext if none is provided
        if (DataContext == null)
        {
            DataContext = new ConditionEditorViewModel();
        }
    }

    private void ConditionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
        {
            var conditionType = item.Tag?.ToString();
            if (!string.IsNullOrEmpty(conditionType) && DataContext is ConditionEditorViewModel vm)
            {
                vm.ChangeConditionTypeCommand.Execute(conditionType);
            }
        }
    }
}
