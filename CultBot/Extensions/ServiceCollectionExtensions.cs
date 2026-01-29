using CultBot.Data;
using CultBot.Services;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CultBot.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCultBotDatabase(this IServiceCollection services)
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = Discord.GatewayIntents.Guilds |
                Discord.GatewayIntents.GuildMembers |
                Discord.GatewayIntents.GuildMessages |
                Discord.GatewayIntents.MessageContent |
                Discord.GatewayIntents.GuildPresences,
            AlwaysDownloadUsers = true
        };
        var client = new DiscordSocketClient(config);
        services.AddSingleton(client);

        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("Warning: DATABASE_URL not set. Using in-memory database for testing.");
            services.AddDbContextFactory<CultBotDbContext>(options =>
                options.UseInMemoryDatabase("CultBotDb"));
        }
        else
        {
            connectionString = ParseRailwayConnectionString(connectionString);
            services.AddDbContextFactory<CultBotDbContext>(options =>
                options.UseNpgsql(connectionString));
        }

        return services;
    }

    public static IServiceCollection AddCultBotServices(this IServiceCollection services)
    {
        services.AddSingleton<IBotReadySignal, BotReadySignal>();

        services.AddSingleton<InitiationService>();
        services.AddSingleton<ConfigurationValidator>();

        services.AddSingleton<ModerationService>();
        services.AddSingleton<SpamDetectionService>();
        services.AddSingleton<ProfanityFilterService>();
        services.AddSingleton<DataCollectionService>();

        services.AddSingleton<OnboardingService>();

        services.AddSingleton<YouTubeLiveService>();
        services.AddSingleton<LiveStreamAnnouncementService>();
        services.AddSingleton<SlashCommandHandler>();

        services.AddHostedService<InitiationExpirationService>();
        services.AddHostedService<LiveStreamCheckerService>();

        services.AddHostedService<BotService>();

        return services;
    }

    private static string ParseRailwayConnectionString(string databaseUrl)
    {
        if (databaseUrl.StartsWith("postgres://"))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
        }
        return databaseUrl;
    }
}
