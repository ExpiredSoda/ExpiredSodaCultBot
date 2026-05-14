namespace CultBot.Features.Memes;

public class PostedMeme
{
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong? DiscordMessageId { get; set; }
    public string Source { get; set; } = "Tumblr";
    public string SourcePostId { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageSha256 { get; set; } = string.Empty;
    public DateTime ScheduledSlotUtc { get; set; }
    public string ScheduledSlotLocal { get; set; } = string.Empty;
    public DateTime PostedAtUtc { get; set; }
}
