using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using TidyUp.Models.Enums;

namespace TidyUp.Models.Domain;

/// <summary>
/// Base class for all file actions.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(MoveFileAction), "move")]
[JsonDerivedType(typeof(CopyFileAction), "copy")]
[JsonDerivedType(typeof(RenameFileAction), "rename")]
[JsonDerivedType(typeof(DeleteFileAction), "delete")]
[JsonDerivedType(typeof(ChangeExtensionAction), "changeExtension")]
[JsonDerivedType(typeof(ExtractArchiveAction), "extractArchive")]
[JsonDerivedType(typeof(RunCommandAction), "runCommand")]
public abstract partial class FileAction : ObservableObject
{
    /// <summary>
    /// Unique identifier for the action.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Type of action.
    /// </summary>
    public abstract ActionType Type { get; }

    /// <summary>
    /// Order in which the action should be executed.
    /// </summary>
    [ObservableProperty]
    private int _order;
}

/// <summary>
/// Action to move a file to a different location.
/// </summary>
public partial class MoveFileAction : FileAction
{
    public override ActionType Type => ActionType.Move;

    /// <summary>
    /// Destination folder path (can include variables).
    /// </summary>
    [ObservableProperty]
    private string _destinationPath = string.Empty;

    /// <summary>
    /// Whether to preserve the original subfolder structure.
    /// </summary>
    [ObservableProperty]
    private bool _preserveSubfolderStructure;

    /// <summary>
    /// How to handle conflicts when destination file exists.
    /// </summary>
    [ObservableProperty]
    private ConflictResolution _conflictResolution = ConflictResolution.Skip;

    /// <summary>
    /// Whether to remove empty source folders after moving.
    /// </summary>
    [ObservableProperty]
    private bool _removeEmptyFolders;
}

/// <summary>
/// Action to copy a file to a different location.
/// </summary>
public partial class CopyFileAction : FileAction
{
    public override ActionType Type => ActionType.Copy;

    /// <summary>
    /// Destination folder path (can include variables).
    /// </summary>
    [ObservableProperty]
    private string _destinationPath = string.Empty;

    /// <summary>
    /// Whether to preserve the original subfolder structure.
    /// </summary>
    [ObservableProperty]
    private bool _preserveSubfolderStructure;

    /// <summary>
    /// How to handle conflicts when destination file exists.
    /// </summary>
    [ObservableProperty]
    private ConflictResolution _conflictResolution = ConflictResolution.Skip;

    /// <summary>
    /// Whether subsequent actions apply to the source or copied file.
    /// </summary>
    [ObservableProperty]
    private bool _applyToSourceFile = true;
}

/// <summary>
/// Action to rename a file.
/// </summary>
public partial class RenameFileAction : FileAction
{
    public override ActionType Type => ActionType.Rename;

    /// <summary>
    /// Name pattern with variable interpolation (e.g., "{filename}_{date}").
    /// </summary>
    [ObservableProperty]
    private string _namePattern = string.Empty;

    /// <summary>
    /// How to handle conflicts when destination file exists.
    /// </summary>
    [ObservableProperty]
    private ConflictResolution _conflictResolution = ConflictResolution.RenameNew;
}

/// <summary>
/// Action to delete a file.
/// </summary>
public partial class DeleteFileAction : FileAction
{
    public override ActionType Type => ActionType.Delete;

    /// <summary>
    /// Whether to use Recycle Bin instead of permanent deletion.
    /// </summary>
    [ObservableProperty]
    private bool _useRecycleBin = true;

    /// <summary>
    /// Whether to remove empty parent folders after deletion.
    /// </summary>
    [ObservableProperty]
    private bool _removeEmptyFolders;

    /// <summary>
    /// Whether to prompt for confirmation before deleting.
    /// </summary>
    [ObservableProperty]
    private bool _confirmBeforeDelete = true;
}

/// <summary>
/// Action to change a file's extension.
/// </summary>
public partial class ChangeExtensionAction : FileAction
{
    public override ActionType Type => ActionType.ChangeExtension;

    /// <summary>
    /// New file extension (without the dot).
    /// </summary>
    [ObservableProperty]
    private string _newExtension = string.Empty;

    /// <summary>
    /// How to handle conflicts when destination file exists.
    /// </summary>
    [ObservableProperty]
    private ConflictResolution _conflictResolution = ConflictResolution.RenameNew;
}

/// <summary>
/// Action to extract files from an archive.
/// </summary>
public partial class ExtractArchiveAction : FileAction
{
    public override ActionType Type => ActionType.ExtractArchive;

    /// <summary>
    /// Destination folder for extracted files (can include variables).
    /// </summary>
    [ObservableProperty]
    private string _destinationPath = string.Empty;

    /// <summary>
    /// Whether to delete the archive after successful extraction.
    /// </summary>
    [ObservableProperty]
    private bool _deleteAfterExtraction;

    /// <summary>
    /// Whether to overwrite existing files in the destination.
    /// </summary>
    [ObservableProperty]
    private bool _overwriteExisting;
}

/// <summary>
/// Action to run a command with the file as context.
/// </summary>
public partial class RunCommandAction : FileAction
{
    public override ActionType Type => ActionType.RunCommand;

    /// <summary>
    /// Command to execute (can include variables).
    /// </summary>
    [ObservableProperty]
    private string _command = string.Empty;

    /// <summary>
    /// Working directory for the command (optional).
    /// </summary>
    [ObservableProperty]
    private string? _workingDirectory;

    /// <summary>
    /// Whether to wait for the command to complete.
    /// </summary>
    [ObservableProperty]
    private bool _waitForCompletion = true;

    /// <summary>
    /// Timeout in seconds for command execution.
    /// </summary>
    [ObservableProperty]
    private int _timeoutSeconds = 30;
}
