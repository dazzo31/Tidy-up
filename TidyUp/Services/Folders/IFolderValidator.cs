namespace TidyUp.Services.Folders;

/// <summary>
/// Status representing physical filesystem validation of a folder path.
/// </summary>
public enum FolderValidationStatus
{
    /// <summary>
    /// Directory exists and is accessible for reading and writing.
    /// </summary>
    Accessible,

    /// <summary>
    /// Directory does not exist on the local system or volume.
    /// </summary>
    Missing,

    /// <summary>
    /// Directory exists but current process lacks permission to access or write.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Specified path is invalid, malformed, or not a directory.
    /// </summary>
    InvalidPath
}

/// <summary>
/// Inspection report for physical accessibility and security properties of a folder.
/// </summary>
public class FolderValidationReport
{
    public string Path { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public bool HasReadPermission { get; init; }
    public bool HasWritePermission { get; init; }
    public bool IsSystemProtected { get; init; }
    public FolderValidationStatus Status { get; init; } = FolderValidationStatus.Accessible;
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Probes folder existence, permissions, and system protection.
/// </summary>
public interface IFolderValidator
{
    FolderValidationReport Validate(string path);
    bool IsSystemProtectedDirectory(string path);
}

