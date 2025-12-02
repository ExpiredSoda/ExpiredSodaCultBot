using Discord;
using Discord.WebSocket;

namespace CultBot.Services;

public class SlashCommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly LiveStreamAnnouncementService _liveStreamService;

    public SlashCommandHandler(
        DiscordSocketClient client,
        LiveStreamAnnouncementService liveStreamService)
    {
        _client = client;
        _liveStreamService = liveStreamService;

        _client.Ready += RegisterCommandsAsync;
        _client.SlashCommandExecuted += HandleSlashCommandAsync;
    }

    private async Task RegisterCommandsAsync()
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR registering slash commands: {ex.Message}");
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
                await _liveStreamService.CheckAndAnnounceAsync(isManualTrigger: true);

                // Get response message
                var response = await _liveStreamService.GetManualAnnouncementResponseAsync();

                // Follow up with the result
                await command.FollowupAsync(response, ephemeral: true);

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
