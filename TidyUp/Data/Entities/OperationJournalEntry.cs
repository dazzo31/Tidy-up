using System.ComponentModel.DataAnnotations;

namespace TidyUp.Data.Entities;

/// <summary>
/// Entity representing an executed file operation in the durable SQLite rollback journal.
/// </summary>
public class OperationJournalEntry
{
    [Key]
    public Guid OperationId { get; set; } = Guid.NewGuid();

    public Guid BatchId { get; set; } = Guid.NewGuid();

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    public string ActionType { get; set; } = string.Empty; // Move, Copy, Rename, ChangeExtension, Delete

    [Required]
    [MaxLength(1000)]
    public string OriginalPath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? TargetPath { get; set; }

    [MaxLength(64)]
    public string? PreActionHash { get; set; } // SHA256 of file before operation

    [MaxLength(64)]
    public string? PostActionHash { get; set; } // SHA256 of file after operation

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Completed"; // Completed, RolledBack, Failed

    public DateTime? RolledBackAt { get; set; }

    [MaxLength(2000)]
    public string? Details { get; set; }

    [MaxLength(500)]
    public string? RuleName { get; set; }

    public Guid? RuleId { get; set; }

    public Guid? RuleRevisionId { get; set; }
}

