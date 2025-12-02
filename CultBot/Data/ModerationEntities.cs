namespace CultBot.Data;

public class UserMessage
{
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool WasDeleted { get; set; }
    public bool WasFlagged { get; set; }
    public string? FlagReason { get; set; }
}

public class UserActivity
{
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public string Username { get; set; } = string.Empty;
    
    // Message statistics
    public int TotalMessageCount { get; set; }
    public DateTime LastMessageTime { get; set; }
    public DateTime FirstSeenAt { get; set; }
    
    // Join/Leave tracking
    public DateTime? JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public int JoinCount { get; set; }
    public int LeaveCount { get; set; }
    
    // Game tracking
    public string? CurrentGame { get; set; }
    public DateTime? GameStartedAt { get; set; }
    
    // Moderation data
    public int WarningCount { get; set; }
    public int SlowModeCount { get; set; }
    public bool IsMuted { get; set; }
    public bool IsBanned { get; set; }
    public DateTime? MutedUntil { get; set; }
    
    public DateTime LastUpdated { get; set; }
}

public class GameActivity
{
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TimeSpan? Duration { get; set; }
}

public class ModerationLog
{
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public ulong? ModeratorId { get; set; } // Null if automated
    public string Action { get; set; } = string.Empty; // "Warning", "SlowMode", "Mute", "Ban"
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsAutomated { get; set; }
}

public class SpamTracker
{
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public List<DateTime> RecentMessageTimes { get; set; } = new();
    public int SpamScore { get; set; }
    public DateTime LastSpamCheck { get; set; }
    public bool IsSlowModeActive { get; set; }
    public DateTime? SlowModeUntil { get; set; }
}
