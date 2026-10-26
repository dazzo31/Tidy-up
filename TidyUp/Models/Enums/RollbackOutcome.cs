namespace TidyUp.Models.Enums;

/// <summary>
/// Explicit domain outcome of a rollback operation attempt.
/// Prevents false reporting of successful restoration when only manual instructions were provided.
/// </summary>
public enum RollbackOutcome
{
    /// <summary>
    /// The inverse file operation succeeded completely and postconditions were verified.
    /// </summary>
    Restored,

    /// <summary>
    /// File was moved to Windows Recycle Bin; user must restore via Windows Shell.
    /// Cannot be automatically reversed by application code.
    /// </summary>
    ManualRestoreRequired,

    /// <summary>
    /// Operation cannot be reversed (e.g. permanent deletion, archive extraction, command execution, or target modified).
    /// </summary>
    NotRecoverable,

    /// <summary>
    /// Rollback failed due to a filesystem, permission, or validation error.
    /// </summary>
    Failed
}
