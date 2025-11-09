namespace TidyUp.Models.Enums;

/// <summary>
/// Defines strategies for resolving file conflicts during move/copy/rename operations.
/// </summary>
public enum ConflictResolution
{
    /// <summary>
    /// Skip the operation and leave the original file unchanged.
    /// </summary>
    Skip,

    /// <summary>
    /// Overwrite the existing file with the new file.
    /// </summary>
    Overwrite,

    /// <summary>
    /// Rename the new file by appending a counter (e.g., file(1).txt).
    /// </summary>
    RenameNew,

    /// <summary>
    /// Rename the existing file with a timestamp backup suffix.
    /// </summary>
    RenameOld,

    /// <summary>
    /// Prompt the user to decide how to handle the conflict.
    /// </summary>
    Prompt
}
