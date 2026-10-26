using TidyUp.Models.Enums;

namespace TidyUp.Models.Domain;

/// <summary>
/// Specifies how an action type in a multi-action chain propagates its output
/// as the source path for subsequent dependent actions.
/// </summary>
public enum ActionContinuationType
{
    /// <summary>Next action receives the destination/target path (e.g. Move, Rename, ChangeExtension).</summary>
    UseTargetPath,

    /// <summary>Next action receives the original source path (e.g. Copy when ApplyToSourceFile is true).</summary>
    UseOriginalSourcePath,

    /// <summary>Next action receives the copy destination (e.g. Copy when ApplyToSourceFile is false).</summary>
    UseCopiedTargetPath,

    /// <summary>Action terminates file chain continuation (e.g. Delete). Subsequent actions cannot execute.</summary>
    TerminatesChain,

    /// <summary>Action produces a directory or process execution, not a single file path (e.g. ExtractArchive, RunCommand).</summary>
    NonFileContinuation
}

/// <summary>
/// Domain model resolving continuation semantics for a given FileAction.
/// </summary>
public static class ActionContinuationResolver
{
    /// <summary>
    /// Determines the continuation type for a given action.
    /// </summary>
    public static ActionContinuationType ResolveContinuationType(FileAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return action switch
        {
            MoveFileAction => ActionContinuationType.UseTargetPath,
            RenameFileAction => ActionContinuationType.UseTargetPath,
            ChangeExtensionAction => ActionContinuationType.UseTargetPath,
            CopyFileAction copy => copy.ApplyToSourceFile
                ? ActionContinuationType.UseOriginalSourcePath
                : ActionContinuationType.UseCopiedTargetPath,
            DeleteFileAction => ActionContinuationType.TerminatesChain,
            ExtractArchiveAction => ActionContinuationType.NonFileContinuation,
            RunCommandAction => ActionContinuationType.NonFileContinuation,
            _ => ActionContinuationType.UseTargetPath
        };
    }

    /// <summary>
    /// Computes the effective source path for the next dependent action in a chain.
    /// </summary>
    public static string? ComputeNextSourcePath(
        FileAction currentAction,
        string originalSourcePath,
        string? targetPath)
    {
        var continuation = ResolveContinuationType(currentAction);

        return continuation switch
        {
            ActionContinuationType.UseTargetPath => targetPath,
            ActionContinuationType.UseCopiedTargetPath => targetPath,
            ActionContinuationType.UseOriginalSourcePath => originalSourcePath,
            ActionContinuationType.TerminatesChain => null,
            ActionContinuationType.NonFileContinuation => null,
            _ => targetPath
        };
    }
}
