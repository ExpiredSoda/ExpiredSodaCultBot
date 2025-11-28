namespace CultBot.Configuration;

public static class BotConfig
{
    // Channel IDs - Replace these with your actual channel IDs
    public const ulong GatewayChannelId = 1442967396238229514; // #gateway
    public const ulong RoleRitualChannelId = 0; // #role-ritual - REPLACE THIS

    // Role IDs - Replace these with your actual role IDs
    public const ulong TheUninitiatedRoleId = 0; // REPLACE THIS
    public const ulong SilentWitnessRoleId = 0; // REPLACE THIS
    public const ulong NeonDiscipleRoleId = 0; // REPLACE THIS
    public const ulong VeiledArchivistRoleId = 0; // REPLACE THIS

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
}
