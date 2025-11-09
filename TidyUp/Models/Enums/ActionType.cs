namespace TidyUp.Models.Enums;

/// <summary>
/// Defines the types of actions that can be performed on files.
/// </summary>
public enum ActionType
{
    /// <summary>
    /// Move a file to a different location.
    /// </summary>
    Move,

    /// <summary>
    /// Copy a file to a different location.
    /// </summary>
    Copy,

    /// <summary>
    /// Rename a file.
    /// </summary>
    Rename,

    /// <summary>
    /// Change the file extension.
    /// </summary>
    ChangeExtension,

    /// <summary>
    /// Delete a file or send it to the Recycle Bin.
    /// </summary>
    Delete,

    /// <summary>
    /// Extract files from an archive (zip, rar).
    /// </summary>
    ExtractArchive,

    /// <summary>
    /// Run a command with the file as context.
    /// </summary>
    RunCommand
}
