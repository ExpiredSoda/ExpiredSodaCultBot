namespace CultBot.Features.Memes;

public class MemeRequestUsage
{
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string RequestKind { get; set; } = string.Empty;
    public string CommandName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public string EasternDateKey { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int AttemptsToday { get; set; }
    public int SuccessfulRequestsToday { get; set; }
    public string? Source { get; set; }
    public string? SourcePostId { get; set; }
    public string? ImageUrl { get; set; }
    public string? SourcePermalink { get; set; }
    public string? ImageSha256 { get; set; }
    public ulong? DiscordMessageId { get; set; }
    public ulong? ChannelId { get; set; }
}
