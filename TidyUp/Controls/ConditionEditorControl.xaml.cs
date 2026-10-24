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
        if (e.RemovedItems.Count == 0) return;

        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
        {
            var conditionType = item.Tag?.ToString();
            if (!string.IsNullOrEmpty(conditionType) && DataContext is ConditionEditorViewModel vm)
            {
                if (comboBox.DataContext is Models.Domain.Condition targetCondition)
                {
                    vm.ChangeConditionType(targetCondition, conditionType);
                }
                else
                {
                    vm.ChangeConditionTypeCommand.Execute(conditionType);
                }
            }
        }
    }
}
