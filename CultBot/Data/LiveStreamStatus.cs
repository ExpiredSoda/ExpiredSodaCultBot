namespace CultBot.Data;

public class LiveStreamStatus
{
    public int Id { get; set; }
    public string Platform { get; set; } = "YouTube"; // Future-proof for other platforms
    public string? CurrentVideoId { get; set; }
    public bool IsLive { get; set; }
    public DateTime? LiveStartedAt { get; set; }
    public DateTime LastCheckedAt { get; set; }
    public bool AnnouncementSent { get; set; }
}
