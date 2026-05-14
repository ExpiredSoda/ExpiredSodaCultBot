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
            var commands = BuildCommandProperties();
            await _client.BulkOverwriteGlobalApplicationCommandsAsync(Array.Empty<ApplicationCommandProperties>());

            foreach (var guild in _client.Guilds)
            {
                await guild.BulkOverwriteApplicationCommandAsync(commands);
                Console.WriteLine($"✓ Registered /live, /meme, and /meme-now slash commands in {guild.Name} ({guild.Id})");
            }

            Console.WriteLine("✓ Cleared global slash commands; using guild commands for immediate updates.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR registering slash commands: {ex}");
            return false;
        }
    }

    private static ApplicationCommandProperties[] BuildCommandProperties()
    {
        var liveCommand = new SlashCommandBuilder()
            .WithName("live")
            .WithDescription("Manually trigger a live stream announcement")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
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

        return new ApplicationCommandProperties[] { liveCommand, memeCommand, memeNowCommand };
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        try
        {
            Console.WriteLine($"Received /{command.Data.Name} command from {command.User.Username} ({command.User.Id}).");

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
                await command.RespondAsync("Looking for a meme...", ephemeral: true);

                var guildUser = command.User as SocketGuildUser;
                Console.WriteLine($"Processing /meme-now for guild {guildUser?.Guild.Id.ToString() ?? "unknown"}.");
                var result = await _memePostingService.PostManualMemeAsync(guildUser?.Guild.Id);

                await command.ModifyOriginalResponseAsync(message => message.Content = result.Message);

                Console.WriteLine($"✓ /meme-now command executed by {command.User.Username}. Result: {result.Status}");
            }
            else if (command.Data.Name == "meme")
            {
                await command.RespondAsync("Looking for a meme...", ephemeral: true);

                if (command.User is not SocketGuildUser guildUser)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "Use this command inside the server.");
                    return;
                }

                if (!HasInitiatedRole(guildUser))
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "Only initiated members can request memes.");
                    return;
                }

                var result = await _memePostingService.PostUserRequestedMemeAsync(guildUser.Id, guildUser.Guild.Id);

                await command.ModifyOriginalResponseAsync(message => message.Content = result.Message);

                Console.WriteLine($"✓ /meme command executed by {command.User.Username}. Result: {result.Status}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR handling /{command.Data.Name} slash command: {ex}");
            try
            {
                if (command.HasResponded)
                {
                    await command.FollowupAsync("An error occurred while processing the command.", ephemeral: true);
                }
                else
                {
                    await command.RespondAsync("An error occurred while processing the command.", ephemeral: true);
                }
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
