using Microsoft.EntityFrameworkCore;
using TidyUp.Data.Entities;

namespace TidyUp.Data;

/// <summary>
/// Database context for TidyUp application.
/// </summary>
public class TidyUpDbContext : DbContext
{
    public TidyUpDbContext(DbContextOptions<TidyUpDbContext> options) : base(options)
    {
    }

    public DbSet<RuleEntity> Rules { get; set; } = null!;
    public DbSet<ProcessedFileEntity> ProcessedFiles { get; set; } = null!;
    public DbSet<ActionLogEntity> ActionLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure indexes for performance
        modelBuilder.Entity<ActionLogEntity>()
            .HasIndex(e => e.Timestamp);

        modelBuilder.Entity<ActionLogEntity>()
            .HasIndex(e => e.RuleId);

        modelBuilder.Entity<ProcessedFileEntity>()
            .HasIndex(e => new { e.FilePath, e.RuleId })
            .IsUnique();

        modelBuilder.Entity<RuleEntity>()
            .HasIndex(e => e.ExecutionOrder);
    }
}
