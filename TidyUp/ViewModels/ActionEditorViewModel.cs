using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for editing rule actions.
/// </summary>
public partial class ActionEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FileAction> _actions = new();

    [ObservableProperty]
    private FileAction? _selectedAction;

    /// <summary>
    /// Adds a new move action.
    /// </summary>
    [RelayCommand]
    private void AddMoveAction()
    {
        var action = new MoveFileAction
        {
            Order = Actions.Count,
            DestinationPath = "",
            ConflictResolution = ConflictResolution.Skip
        };
        Actions.Add(action);
        SelectedAction = action;
    }

    /// <summary>
    /// Adds a new copy action.
    /// </summary>
    [RelayCommand]
    private void AddCopyAction()
    {
        var action = new CopyFileAction
        {
            Order = Actions.Count,
            DestinationPath = "",
            ConflictResolution = ConflictResolution.Skip,
            ApplyToSourceFile = true
        };
        Actions.Add(action);
        SelectedAction = action;
    }

    /// <summary>
    /// Adds a new rename action.
    /// </summary>
    [RelayCommand]
    private void AddRenameAction()
    {
        var action = new RenameFileAction
        {
            Order = Actions.Count,
            NamePattern = "{filename}",
            ConflictResolution = ConflictResolution.RenameNew
        };
        Actions.Add(action);
        SelectedAction = action;
    }

    /// <summary>
    /// Adds a new delete action.
    /// </summary>
    [RelayCommand]
    private void AddDeleteAction()
    {
        var action = new DeleteFileAction
        {
            Order = Actions.Count,
            UseRecycleBin = true,
            ConfirmBeforeDelete = true
        };
        Actions.Add(action);
        SelectedAction = action;
    }

    /// <summary>
    /// Adds a new change extension action.
    /// </summary>
    [RelayCommand]
    private void AddChangeExtensionAction()
    {
        var action = new ChangeExtensionAction
        {
            Order = Actions.Count,
            NewExtension = "",
            ConflictResolution = ConflictResolution.RenameNew
        };
        Actions.Add(action);
        SelectedAction = action;
    }

    /// <summary>
    /// Adds a new extract archive action.
    /// </summary>
    [RelayCommand]
    private void AddExtractArchiveAction()
    {
        var action = new ExtractArchiveAction
        {
            Order = Actions.Count,
            DestinationPath = "",
            OverwriteExisting = false,
            DeleteAfterExtraction = false
        };
        Actions.Add(action);
        SelectedAction = action;
    }

    /// <summary>
    /// Adds a new run command action.
    /// </summary>
    [RelayCommand]
    private void AddRunCommandAction()
    {
        var action = new RunCommandAction
        {
            Order = Actions.Count,
            Command = "",
            WaitForCompletion = true,
            TimeoutSeconds = 30
        };
        Actions.Add(action);
        SelectedAction = action;
    }

    /// <summary>
    /// Removes the selected action.
    /// </summary>
    [RelayCommand]
    private void RemoveAction(FileAction? action)
    {
        if (action == null) return;

        Actions.Remove(action);
        
        // Reorder remaining actions
        for (int i = 0; i < Actions.Count; i++)
        {
            Actions[i].Order = i;
        }
    }

    /// <summary>
    /// Moves action up in the list.
    /// </summary>
    [RelayCommand]
    private void MoveActionUp(FileAction? action)
    {
        if (action == null) return;

        var index = Actions.IndexOf(action);
        if (index > 0)
        {
            Actions.Move(index, index - 1);
            ReorderActions();
        }
    }

    /// <summary>
    /// Moves action down in the list.
    /// </summary>
    [RelayCommand]
    private void MoveActionDown(FileAction? action)
    {
        if (action == null) return;

        var index = Actions.IndexOf(action);
        if (index < Actions.Count - 1)
        {
            Actions.Move(index, index + 1);
            ReorderActions();
        }
    }

    private void ReorderActions()
    {
        for (int i = 0; i < Actions.Count; i++)
        {
            Actions[i].Order = i;
        }
    }

    /// <summary>
    /// Gets the display name for an action type.
    /// </summary>
    public static string GetActionTypeName(FileAction action)
    {
        return action switch
        {
            MoveFileAction => "Move File",
            CopyFileAction => "Copy File",
            RenameFileAction => "Rename File",
            ChangeExtensionAction => "Change Extension",
            DeleteFileAction => "Delete File",
            ExtractArchiveAction => "Extract Archive",
            RunCommandAction => "Run Command",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets the icon kind for an action type.
    /// </summary>
    public static string GetActionIcon(FileAction action)
    {
        return action switch
        {
            MoveFileAction => "FolderMove",
            CopyFileAction => "ContentCopy",
            RenameFileAction => "Rename",
            ChangeExtensionAction => "FileDocument",
            DeleteFileAction => "Delete",
            ExtractArchiveAction => "FolderZip",
            RunCommandAction => "Console",
            _ => "HelpCircle"
        };
    }
}
