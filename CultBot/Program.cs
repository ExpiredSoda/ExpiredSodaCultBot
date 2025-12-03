using CultBot.Data;
using CultBot.Services;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== Starting ExpiredSodaCultBot ===");
            
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
            {
                try
                {
                    Console.WriteLine("Configuring services...");
                    
                    // Discord client
                    var config = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.Guilds |
                                   GatewayIntents.GuildMembers |
                                   GatewayIntents.GuildMessages |
                                   GatewayIntents.MessageContent |
                                   GatewayIntents.GuildPresences,
                    AlwaysDownloadUsers = true
                };
                var client = new DiscordSocketClient(config);
                services.AddSingleton(client);

                // Database - Read connection string from environment variable
                var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    Console.WriteLine("Warning: DATABASE_URL not set. Using in-memory database for testing.");
                    services.AddDbContextFactory<CultBotDbContext>(options =>
                        options.UseInMemoryDatabase("CultBotDb"));
                }
                else
                {
                    // Railway provides DATABASE_URL in a specific format, we may need to parse it
                    connectionString = ParseRailwayConnectionString(connectionString);
                    services.AddDbContextFactory<CultBotDbContext>(options =>
                        options.UseNpgsql(connectionString));
                }

                // Core Services (in dependency order)
                services.AddSingleton<InitiationService>();
                services.AddSingleton<ConfigurationValidator>();
                
                // Moderation & Data Collection Services (register before services that depend on them)
                services.AddSingleton<ModerationService>();
                services.AddSingleton<SpamDetectionService>();
                services.AddSingleton<ProfanityFilterService>();
                services.AddSingleton<DataCollectionService>();
                
                // Services that depend on DataCollectionService
                services.AddSingleton<OnboardingService>();
                
                // YouTube Live Stream Services
                services.AddSingleton<YouTubeLiveService>();
                services.AddSingleton<LiveStreamAnnouncementService>();
                services.AddSingleton<SlashCommandHandler>();

                // Background services
                services.AddHostedService<InitiationExpirationService>();
                services.AddHostedService<LiveStreamCheckerService>();

                // Bot service
                services.AddHostedService<BotService>();
                
                Console.WriteLine("✓ All services configured");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR during service configuration:");
                    Console.WriteLine($"  Type: {ex.GetType().Name}");
                    Console.WriteLine($"  Message: {ex.Message}");
                    throw;
                }
            })
            .Build();

            Console.WriteLine("✓ Host built successfully");
            Console.WriteLine("Starting host...");
            
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("=== FATAL STARTUP ERROR ===");
            Console.WriteLine($"Exception Type: {ex.GetType().FullName}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Source: {ex.Source}");
            Console.WriteLine($"StackTrace:\n{ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine("\n=== INNER EXCEPTION ===");
                Console.WriteLine($"Type: {ex.InnerException.GetType().FullName}");
                Console.WriteLine($"Message: {ex.InnerException.Message}");
                Console.WriteLine($"StackTrace:\n{ex.InnerException.StackTrace}");
            }
            
            throw;
        }
    }

    private static string ParseRailwayConnectionString(string databaseUrl)
    {
        // Railway provides DATABASE_URL in format: postgres://user:password@host:port/database
        // Npgsql expects: Host=host;Port=port;Database=database;Username=user;Password=password
        
        if (databaseUrl.StartsWith("postgres://"))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
        }

        return databaseUrl;
    }
}

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

    public BotService(
        DiscordSocketClient client,
        OnboardingService onboardingService,
        IDbContextFactory<CultBotDbContext> contextFactory,
        ConfigurationValidator configValidator,
        SlashCommandHandler slashCommandHandler,
        ModerationService moderationService,
        SpamDetectionService spamDetectionService,
        ProfanityFilterService profanityFilterService,
        DataCollectionService dataCollectionService)
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
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Ensure database is created
        try
        {
            await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Database.EnsureCreatedAsync(cancellationToken);
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
        
        // Run configuration validation
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
            await userMessage.DeleteAsync();
            var warning = await userMessage.Channel.SendMessageAsync($"{guildUser.Mention}, you are in slow mode. Please wait before sending another message.");
            _ = Task.Delay(5000).ContinueWith(_ => warning.DeleteAsync());
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
            await _onboardingService.HandleButtonInteractionAsync(component);
        }
    }
}
