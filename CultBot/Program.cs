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
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Discord client
                var config = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.Guilds |
                                   GatewayIntents.GuildMembers,
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

                // Services
                services.AddSingleton<InitiationService>();
                services.AddSingleton<OnboardingService>();

                // Background service for expiration checking
                services.AddHostedService<InitiationExpirationService>();

                // Bot service
                services.AddHostedService<BotService>();
            })
            .Build();

        await host.RunAsync();
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

    public BotService(
        DiscordSocketClient client,
        OnboardingService onboardingService,
        IDbContextFactory<CultBotDbContext> contextFactory)
    {
        _client = client;
        _onboardingService = onboardingService;
        _contextFactory = contextFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Ensure database is created
        await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
            Console.WriteLine("Database initialized.");
        }

        // Wire up event handlers
        _client.Log += LogAsync;
        _client.Ready += OnReadyAsync;
        _client.UserJoined += OnUserJoinedAsync;
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

    private Task OnReadyAsync()
    {
        Console.WriteLine($"Connected as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
        Console.WriteLine("Cult Bot is ready!");
        return Task.CompletedTask;
    }

    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        await _onboardingService.HandleUserJoinedAsync(user);
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        if (interaction is SocketMessageComponent component)
        {
            await _onboardingService.HandleButtonInteractionAsync(component);
        }
    }
}
