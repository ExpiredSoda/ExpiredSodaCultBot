namespace CultBot.Data;

public class InitiationSession
{
    public int Id { get; set; }

    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public ulong RitualChannelId { get; set; }
    public ulong RitualMessageId { get; set; }

    public DateTime JoinTimeUtc { get; set; }

    public string Status { get; set; } = "Pending"; // "Pending", "Completed", "Expired"
    public string? ChosenRole { get; set; } // "SilentWitness", "NeonDisciple", "VeiledArchivist"

    public DateTime? CompletedTimeUtc { get; set; }
    public DateTime? ExpiredTimeUtc { get; set; }
}
