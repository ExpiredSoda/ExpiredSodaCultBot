using CultBot.Configuration;
using CultBot.Features.Memes;
using Discord;
using Discord.WebSocket;

namespace CultBot.Services;

public class SlashCommandHandler
{
    private static bool _commandsRegistered;

    private readonly DiscordSocketClient _client;
    private readonly LiveStreamAnnouncementService _liveStreamService;
    private readonly MemePostingService _memePostingService;

    public SlashCommandHandler(
        DiscordSocketClient client,
        LiveStreamAnnouncementService liveStreamService,
        MemePostingService memePostingService)
    {
        _client = client;
        _liveStreamService = liveStreamService;
        _memePostingService = memePostingService;

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

            var memeNowCommand = new SlashCommandBuilder()
                .WithName("meme-now")
                .WithDescription("Post one image meme now")
                .WithDefaultMemberPermissions(GuildPermission.Administrator)
                .Build();

            var memeCommand = new SlashCommandBuilder()
                .WithName("meme")
                .WithDescription("Request one image meme")
                .Build();

            await _client.BulkOverwriteGlobalApplicationCommandsAsync(new[] { liveCommand, memeCommand, memeNowCommand });

            Console.WriteLine("✓ Registered /live, /meme, and /meme-now slash commands");
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
            else if (command.Data.Name == "meme-now")
            {
                await command.DeferAsync(ephemeral: true);

                var guildUser = command.User as SocketGuildUser;
                var result = await _memePostingService.PostManualMemeAsync(guildUser?.Guild.Id);

                await command.FollowupAsync(result.Message, ephemeral: true);

                Console.WriteLine($"✓ /meme-now command executed by {command.User.Username}");
            }
            else if (command.Data.Name == "meme")
            {
                await command.DeferAsync(ephemeral: true);

                if (command.User is not SocketGuildUser guildUser)
                {
                    await command.FollowupAsync("Use this command inside the server.", ephemeral: true);
                    return;
                }

                if (!HasInitiatedRole(guildUser))
                {
                    await command.FollowupAsync("Only initiated members can request memes.", ephemeral: true);
                    return;
                }

                var result = await _memePostingService.PostUserRequestedMemeAsync(guildUser.Id, guildUser.Guild.Id);

                await command.FollowupAsync(result.Message, ephemeral: true);

                Console.WriteLine($"✓ /meme command executed by {command.User.Username}");
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

    private static bool HasInitiatedRole(SocketGuildUser user)
    {
        return user.Roles.Any(role =>
            role.Id == BotConfig.SilentWitnessRoleId ||
            role.Id == BotConfig.NeonDiscipleRoleId ||
            role.Id == BotConfig.VeiledArchivistRoleId);
    }
}
