namespace TidyUp.Models.Domain;

/// <summary>
/// Represents a file that has been processed by a rule.
/// </summary>
public class ProcessedFile
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID of the rule that processed this file.
    /// </summary>
    public Guid RuleId { get; set; }

    /// <summary>
    /// Full path to the file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the file content.
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// When the file was last processed.
    /// </summary>
    public DateTime LastProcessed { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// File size in bytes at time of processing.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Whether the file is marked for reprocessing.
    /// </summary>
    public bool IsPending { get; set; }
}
