using CultBot.Data;
using CultBot.Features.Memes;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace CultBot.Services;

public class BotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly OnboardingService _onboardingService;
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;
    private readonly ConfigurationValidator _configValidator;
    private readonly SlashCommandHandler _slashCommandHandler;
    private readonly ModerationService _moderationService;
    private readonly SpamDetectionService _spamDetectionService;
    private readonly ProfanityFilterService _profanityFilterService;
    private readonly DataCollectionService _dataCollectionService;
    private readonly GiveawayService _giveawayService;
    private readonly IBotReadySignal _readySignal;
    private readonly MemeSchemaInitializer _memeSchemaInitializer;

    public BotService(
        DiscordSocketClient client,
        OnboardingService onboardingService,
        IDbContextFactory<CultBotDbContext> contextFactory,
        ConfigurationValidator configValidator,
        SlashCommandHandler slashCommandHandler,
        ModerationService moderationService,
        SpamDetectionService spamDetectionService,
        ProfanityFilterService profanityFilterService,
        DataCollectionService dataCollectionService,
        GiveawayService giveawayService,
        IBotReadySignal readySignal,
        MemeSchemaInitializer memeSchemaInitializer)
    {
        _client = client;
        _onboardingService = onboardingService;
        _contextFactory = contextFactory;
        _configValidator = configValidator;
        _slashCommandHandler = slashCommandHandler;
        _moderationService = moderationService;
        _spamDetectionService = spamDetectionService;
        _profanityFilterService = profanityFilterService;
        _dataCollectionService = dataCollectionService;
        _giveawayService = giveawayService;
        _readySignal = readySignal;
        _memeSchemaInitializer = memeSchemaInitializer;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Ensure database is created
        try
        {
            await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Database.EnsureCreatedAsync(cancellationToken);
                await _memeSchemaInitializer.EnsureCreatedAsync(cancellationToken);
                Console.WriteLine("✓ Database initialized successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL ERROR initializing database: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            throw;
        }

        // Wire up event handlers
        _client.Log += LogAsync;
        _client.Ready += OnReadyAsync;
        _client.UserJoined += OnUserJoinedAsync;
        _client.UserLeft += OnUserLeftAsync;
        _client.MessageReceived += OnMessageReceivedAsync;
        _client.PresenceUpdated += OnPresenceUpdatedAsync;
        _client.InteractionCreated += OnInteractionCreatedAsync;

        // Read bot token from environment variable
        var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("ERROR: Bot token not set. Set the DISCORD_BOT_TOKEN environment variable.");
            throw new InvalidOperationException("DISCORD_BOT_TOKEN environment variable is not set.");
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        Console.WriteLine("Cult Bot is starting...");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
        Console.WriteLine("Cult Bot stopped.");
    }

    private Task LogAsync(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task OnReadyAsync()
    {
        Console.WriteLine($"Connected as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
        Console.WriteLine("Cult Bot is ready!");

        await _slashCommandHandler.RegisterCommandsOnceAsync();
        _readySignal.SetReady();

        await _configValidator.ValidateConfigurationAsync();
    }

    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        await _onboardingService.HandleUserJoinedAsync(user);
    }

    private async Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        await _dataCollectionService.TrackUserLeaveAsync(guild, user);
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage) return;
        if (userMessage.Author.IsBot) return;

        var guildUser = userMessage.Author as SocketGuildUser;
        if (guildUser == null) return;

        // Check if user is in slow mode
        var isInSlowMode = await _moderationService.IsUserInSlowModeAsync(guildUser.Id, guildUser.Guild.Id);
        if (isInSlowMode)
        {
            try
            {
                await userMessage.DeleteAsync();
                var warning = await userMessage.Channel.SendMessageAsync($"{guildUser.Mention}, you are in slow mode. Please wait before sending another message.");
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    try
                    {
                        await warning.DeleteAsync();
                    }
                    catch
                    {
                        // Warning may already be gone, or the bot may lack cleanup permissions.
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR enforcing slow mode for {guildUser.Username}: {ex.Message}");
            }
            return;
        }

        // Track message for data collection
        await _dataCollectionService.TrackMessageAsync(userMessage);

        // Check for profanity
        var hasProfanity = await _profanityFilterService.CheckMessageForProfanityAsync(userMessage);
        if (hasProfanity) return; // Message already handled by profanity filter

        // Check for spam
        await _spamDetectionService.CheckForSpamAsync(userMessage);
    }

    private async Task OnPresenceUpdatedAsync(SocketUser user, SocketPresence before, SocketPresence after)
    {
        if (user is not SocketGuildUser guildUser) return;

        // Check if game activity changed
        var beforeGame = before.Activities.FirstOrDefault(a => a.Type == ActivityType.Playing);
        var afterGame = after.Activities.FirstOrDefault(a => a.Type == ActivityType.Playing);

        if (beforeGame?.Name != afterGame?.Name)
        {
            if (afterGame != null)
            {
                await _dataCollectionService.TrackGameActivityAsync(guildUser, afterGame);
            }
            else if (beforeGame != null)
            {
                await _dataCollectionService.EndGameSessionAsync(guildUser);
            }
        }
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        if (interaction is SocketMessageComponent component)
        {
            var handled = await _giveawayService.HandleDrawButtonAsync(component);
            if (handled)
                return;
            await _onboardingService.HandleButtonInteractionAsync(component);
        }
    }
}
