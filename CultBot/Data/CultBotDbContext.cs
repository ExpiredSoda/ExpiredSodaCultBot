using Microsoft.EntityFrameworkCore;

namespace CultBot.Data;

public class CultBotDbContext : DbContext
{
    public CultBotDbContext(DbContextOptions<CultBotDbContext> options) : base(options)
    {
    }

    public DbSet<InitiationSession> InitiationSessions { get; set; } = null!;
    public DbSet<LiveStreamStatus> LiveStreamStatuses { get; set; } = null!;
    
    // Moderation & Data Collection
    public DbSet<UserMessage> UserMessages { get; set; } = null!;
    public DbSet<UserActivity> UserActivities { get; set; } = null!;
    public DbSet<GameActivity> GameActivities { get; set; } = null!;
    public DbSet<ModerationLog> ModerationLogs { get; set; } = null!;
    public DbSet<SpamTracker> SpamTrackers { get; set; } = null!;

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

        modelBuilder.Entity<UserMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.GuildId });
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<UserActivity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.GuildId }).IsUnique();
            entity.HasIndex(e => e.TotalMessageCount);
        });

        modelBuilder.Entity<GameActivity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.GuildId });
            entity.HasIndex(e => e.GameName);
        });

        modelBuilder.Entity<ModerationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.GuildId });
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<SpamTracker>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.GuildId }).IsUnique();
        });
    }
}
