using System.Windows;
using System.Windows.Controls;
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
}
