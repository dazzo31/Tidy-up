using System.ComponentModel.DataAnnotations;

namespace TidyUp.Data.Entities;

/// <summary>
/// Entity for logging file actions performed by rules.
/// </summary>
public class ActionLogEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid RuleId { get; set; }

    [Required]
    [MaxLength(500)]
    public string RuleName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ActionPerformed { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ResultPath { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty; // Success, Warning, Error

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }
}
