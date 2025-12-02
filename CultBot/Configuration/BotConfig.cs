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
}
