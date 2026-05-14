namespace CultBot.Features.Memes;

public class MemeRequestUsage
{
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public string EasternDateKey { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
