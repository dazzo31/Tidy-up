using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TidyUp.Data.Entities;

/// <summary>
/// Entity for tracking files that have been processed.
/// </summary>
[Index(nameof(FilePath), nameof(RuleId))]
public class ProcessedFileEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RuleId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    public DateTime LastProcessed { get; set; } = DateTime.UtcNow;

    public long FileSize { get; set; }

    public bool IsPending { get; set; }
}
