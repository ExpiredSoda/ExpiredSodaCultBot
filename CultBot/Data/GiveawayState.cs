namespace CultBot.Data;

public class GiveawayState
{
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public int CurrentGoal { get; set; }
    public int? LastGiveawayGoalReached { get; set; }
    public ulong? ProgressMessageId { get; set; }
    /// <summary>Last count we put in the progress message (skip update if count unchanged).</summary>
    public int? LastAnnouncedCount { get; set; }
    /// <summary>When we last sent a weekly update message.</summary>
    public DateTime? LastWeeklyUpdateAt { get; set; }
    /// <summary>Message ID of the "goal reached" draw button (waiting for host to press).</summary>
    public ulong? PendingDrawMessageId { get; set; }
}
