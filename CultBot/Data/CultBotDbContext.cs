using CultBot.Features.Memes;
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
    public DbSet<GiveawayState> GiveawayStates { get; set; } = null!;
    public DbSet<PostedMeme> PostedMemes { get; set; } = null!;
    public DbSet<MemeRequestUsage> MemeRequestUsages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InitiationSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.GuildId, e.Status });
            entity.HasIndex(e => new { e.UserId, e.GuildId })
                .IsUnique()
                .HasFilter("\"Status\" = 'Pending'");
            entity.HasIndex(e => new { e.GuildId, e.RitualMessageId })
                .IsUnique();
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<LiveStreamStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Platform).IsUnique();
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
            
            // Store list as JSON in PostgreSQL
            entity.Property(e => e.RecentMessageTimes)
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<GiveawayState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GuildId).IsUnique();
        });

        modelBuilder.Entity<PostedMeme>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GuildId, e.Source, e.SourcePostId }).IsUnique();
            entity.HasIndex(e => new { e.GuildId, e.ImageSha256 }).IsUnique();
            entity.HasIndex(e => new { e.GuildId, e.ScheduledSlotUtc }).IsUnique();
            entity.HasIndex(e => e.PostedAtUtc);
        });

        modelBuilder.Entity<MemeRequestUsage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.EasternDateKey, e.Result });
            entity.HasIndex(e => new { e.UserId, e.RequestedAtUtc });
        });
    }
}
