using Microsoft.EntityFrameworkCore;

namespace CultBot.Data;

public class CultBotDbContext : DbContext
{
    public CultBotDbContext(DbContextOptions<CultBotDbContext> options) : base(options)
    {
    }

    public DbSet<InitiationSession> InitiationSessions { get; set; } = null!;
    public DbSet<LiveStreamStatus> LiveStreamStatuses { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InitiationSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.GuildId });
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<LiveStreamStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Platform);
        });
    }
}
