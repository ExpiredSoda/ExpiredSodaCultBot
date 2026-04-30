using Discord;
using Discord.WebSocket;

namespace CultBot.Services;

public class SlashCommandHandler
{
    private static bool _commandsRegistered;

    private readonly DiscordSocketClient _client;
    private readonly LiveStreamAnnouncementService _liveStreamService;

    public SlashCommandHandler(
        DiscordSocketClient client,
        LiveStreamAnnouncementService liveStreamService)
    {
        _client = client;
        _liveStreamService = liveStreamService;

        _client.SlashCommandExecuted += HandleSlashCommandAsync;
    }

    /// <summary>Register slash commands once. Call from BotService.OnReadyAsync; does nothing on subsequent calls.</summary>
    public async Task RegisterCommandsOnceAsync()
    {
        if (_commandsRegistered)
            return;
        _commandsRegistered = await RegisterCommandsAsync();
    }

    private async Task<bool> RegisterCommandsAsync()
    {
        try
        {
            var liveCommand = new SlashCommandBuilder()
                .WithName("live")
                .WithDescription("Manually trigger a live stream announcement")
                .WithDefaultMemberPermissions(GuildPermission.Administrator) // Only admins can use this
                .Build();

            // Register globally (available in all servers)
            await _client.CreateGlobalApplicationCommandAsync(liveCommand);
            
            Console.WriteLine("✓ Registered /live slash command");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR registering slash commands: {ex.Message}");
            return false;
        }
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        try
        {
            if (command.Data.Name == "live")
            {
                // Defer the response since YouTube API call might take a moment
                await command.DeferAsync(ephemeral: true);

                // Check if live and send announcement
                var result = await _liveStreamService.CheckAndAnnounceAsync(isManualTrigger: true);

                // Follow up with the result
                await command.FollowupAsync(result.Message, ephemeral: true);

                Console.WriteLine($"✓ /live command executed by {command.User.Username}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR handling slash command: {ex.Message}");
            try
            {
                await command.FollowupAsync("An error occurred while processing the command.", ephemeral: true);
            }
            catch
            {
                // Command may have already been acknowledged
            }
        }
    }
}
