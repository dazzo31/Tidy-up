using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using TidyUp.Models.Domain;
using TidyUp.ViewModels;

namespace TidyUp.Controls;

/// <summary>
/// Interaction logic for ActionEditorControl.xaml
/// </summary>
public partial class ActionEditorControl : UserControl
{
    public ActionEditorControl()
    {
        InitializeComponent();
        
        // Set default DataContext if none is provided
        if (DataContext == null)
        {
            DataContext = new ActionEditorViewModel();
        }
    }

    private void AddActionButton_Click(object sender, RoutedEventArgs e)
    {
        // Open the context menu when button is clicked
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void BrowseDestinationFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not FileAction action)
            return;

        var dialog = new OpenFolderDialog
        {
            Title = "Select Destination Folder",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            // Set destination path based on action type
            switch (action)
            {
                case MoveFileAction moveAction:
                    moveAction.DestinationPath = dialog.FolderName;
                    break;
                case CopyFileAction copyAction:
                    copyAction.DestinationPath = dialog.FolderName;
                    break;
            }
        }
    }

    private void InsertVariable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string textBoxName)
            return;

        // Find the target TextBox by name in the visual tree
        var textBox = FindTextBoxByName(button, textBoxName);
        if (textBox == null)
            return;

        // Create popup with variable inserter
        var popup = new Popup
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true
        };

        var inserter = new VariableInserterPopup();
        inserter.VariableSelected += (s, variable) =>
        {
            // Insert at cursor position or append
            var caretIndex = textBox.CaretIndex;
            var currentText = textBox.Text ?? string.Empty;
            
            var newText = currentText.Insert(caretIndex, variable);
            textBox.Text = newText;
            textBox.CaretIndex = caretIndex + variable.Length;
            textBox.Focus();
        };

        popup.Child = inserter;
        popup.IsOpen = true;
    }

    private TextBox? FindTextBoxByName(DependencyObject parent, string name)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(parent);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // Check if this is a TextBox with the matching name
            if (current is TextBox textBox && textBox.Name == name)
                return textBox;

            // Add children to queue
            var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(current, i);
                queue.Enqueue(child);
            }
        }

        return null;
    }
}
