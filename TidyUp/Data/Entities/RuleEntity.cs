using System.ComponentModel.DataAnnotations;

namespace TidyUp.Data.Entities;

/// <summary>
/// Entity Framework entity for storing rules in the database.
/// </summary>
public class RuleEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// JSON-serialized rule configuration (MonitoredFolders, Conditions, Actions).
    /// </summary>
    [Required]
    public string ConfigJson { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public int ExecutionOrder { get; set; }

    public bool StopProcessingAfterMatch { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    public DateTime? LastRunDate { get; set; }

    public int FilesProcessedCount { get; set; }
}
