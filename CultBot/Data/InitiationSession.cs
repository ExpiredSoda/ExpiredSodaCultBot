namespace CultBot.Data;

/// <summary>Constants for InitiationSession.Status.</summary>
public static class InitiationSessionStatus
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Expired = "Expired";
}

public class InitiationSession
{
    public int Id { get; set; }

    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public ulong RitualChannelId { get; set; }
    public ulong RitualMessageId { get; set; }

    public DateTime JoinTimeUtc { get; set; }

    public string Status { get; set; } = InitiationSessionStatus.Pending;
    public string? ChosenRole { get; set; } // "SilentWitness", "NeonDisciple", "VeiledArchivist"

    public DateTime? CompletedTimeUtc { get; set; }
    public DateTime? ExpiredTimeUtc { get; set; }
}
