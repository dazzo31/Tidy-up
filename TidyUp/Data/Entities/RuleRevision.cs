using System.ComponentModel.DataAnnotations;

namespace TidyUp.Data.Entities;

/// <summary>
/// Immutable historical snapshot of a rule revision.
/// </summary>
public class RuleRevision
{
    [Key]
    public Guid RevisionId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid RuleId { get; set; }

    public int VersionNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Complete JSON snapshot of the rule configuration at this revision.
    /// </summary>
    [Required]
    public string SerializedRuleJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? ChangeDescription { get; set; }
}

