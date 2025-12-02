namespace CultBot.Configuration;

public static class BotConfig
{
    // Channel IDs - Replace these with your actual channel IDs
    public const ulong GatewayChannelId = 1442967396238229514; 
    public const ulong RoleRitualChannelId = 1442967445873889442;

    // Role IDs - Replace these with your actual role IDs
    public const ulong TheUninitiatedRoleId = 1443893711057715301;
    public const ulong SilentWitnessRoleId = 1443894293218594918;
    public const ulong NeonDiscipleRoleId = 1443894391444996096;
    public const ulong VeiledArchivistRoleId = 1443894536223850496;

    // GIF URLs for successful initiation messages
    public const string SilentWitnessGifUrl = "SILENT_WITNESS_GIF_URL"; // Replace with actual GIF URL
    public const string NeonDiscipleGifUrl = "NEON_DISCIPLE_GIF_URL"; // Replace with actual GIF URL
    public const string VeiledArchivistGifUrl = "VEILED_ARCHIVIST_GIF_URL"; // Replace with actual GIF URL

    // Initiation timeout in hours
    public const int InitiationTimeoutHours = 24;

    // How often to check for expired initiations (in minutes)
    public const int ExpirationCheckIntervalMinutes = 5;

    // Custom IDs for buttons
    public const string ButtonSilentWitness = "ritual_button_silent_witness";
    public const string ButtonNeonDisciple = "ritual_button_neon_disciple";
    public const string ButtonVeiledArchivist = "ritual_button_veiled_archivist";

    // YouTube Live Stream Configuration
    public const ulong TransmissionsChannelId = 0; // #transmissions - REPLACE THIS
    public const string YouTubeChannelHandle = "@ExpiredSodaOfficial";
    public const string YouTubeChannelId = ""; // Will be auto-resolved from handle, or set manually
    public const int LiveCheckIntervalMinutes = 10;

    // Moderation Configuration
    public const ulong ModLogChannelId = 0; // #mod-log - REPLACE THIS (optional)
    
    // Spam Detection Settings
    public const int SpamMessageThreshold = 5; // Messages in time window
    public const int SpamTimeWindowSeconds = 10; // Time window for spam detection
    public const int SpamScoreThreshold = 15; // Score to trigger action
    public const int SlowModeDurationMinutes = 5; // How long to apply slow mode
    public const int BotBanThreshold = 25; // Score to auto-ban suspected bots
    
    // Bot Detection Criteria
    public const int BotAccountAgeThresholdDays = 7; // Account younger than this = suspicious
    public const int BotMessageLengthThreshold = 200; // Extremely long messages
    public const int BotLinkRatio = 3; // Messages with links vs total messages
    
    // Profanity Filter (add your list of terms to detect)
    public static readonly string[] RacialSlurs = Array.Empty<string>();
    // Populate this array with terms appropriate for your server
    // Example: new[] { "term1", "term2", "term3" }
    
    // Game Tracking Keywords (games to track when mentioned in chat)
    public static readonly string[] TrackedGames = new[]
    {
        "valorant", "league of legends", "minecraft", "fortnite", 
        "apex legends", "overwatch", "csgo", "cs2", "dota", 
        "gta", "cod", "warzone", "destiny", "rust"
    };
}
