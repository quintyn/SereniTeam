using Microsoft.EntityFrameworkCore;
using SereniTeam.Shared.Models;

namespace SereniTeam.Server.Data;

/// <summary>
/// Entity Framework context for SereniTeam database
/// </summary>
public class SereniTeamContext : DbContext
{
    public SereniTeamContext(DbContextOptions<SereniTeamContext> options) : base(options)
    {
    }

    public DbSet<Team> Teams { get; set; } = null!;
    public DbSet<CheckIn> CheckIns { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Team entity
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure CheckIn entity
        modelBuilder.Entity<CheckIn>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MoodRating).IsRequired();
            entity.Property(e => e.StressLevel).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.SubmittedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Configure relationship
            entity.HasOne(e => e.Team)
                  .WithMany(t => t.CheckIns)
                  .HasForeignKey(e => e.TeamId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Add indexes for performance
            entity.HasIndex(e => e.TeamId);
            entity.HasIndex(e => e.SubmittedAt);
            entity.HasIndex(e => new { e.TeamId, e.SubmittedAt });
        });
    }
}