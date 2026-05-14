using CultBot.Data;
using CultBot.Features.Memes;
using CultBot.Services;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

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
        else if (connectionString.TrimStart().StartsWith("${{", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "DATABASE_URL appears to be an unresolved Railway variable reference. " +
                "In Railway: open your bot service → Variables → ensure DATABASE_URL is a *reference* to your PostgreSQL service (Add Variable → Add Reference → select your Postgres service → choose DATABASE_URL). " +
                "The reference name must match your PostgreSQL service name exactly (e.g. if the service is named 'PostgreSQL', use ${{PostgreSQL.DATABASE_URL}}).");
        }
        else
        {
            connectionString = ParseRailwayConnectionString(connectionString);
            services.AddSingleton(_ =>
            {
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
                dataSourceBuilder.EnableDynamicJson();
                return dataSourceBuilder.Build();
            });
            services.AddDbContextFactory<CultBotDbContext>((serviceProvider, options) =>
                options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        }

        return services;
    }

    public static IServiceCollection AddCultBotServices(this IServiceCollection services)
    {
        services.AddSingleton<IBotReadySignal, BotReadySignal>();
        services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        });

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
        services.AddSingleton<GiveawayService>();
        services.AddSingleton<TumblrMemeProvider>();
        services.AddSingleton<MemePostingService>();
        services.AddSingleton<MemeSchemaInitializer>();

        services.AddHostedService<InitiationExpirationService>();
        services.AddHostedService<LiveStreamCheckerService>();
        services.AddHostedService<GiveawayBackgroundService>();
        services.AddHostedService<MemeSchedulerService>();

        services.AddHostedService<BotService>();

        return services;
    }

    private static string ParseRailwayConnectionString(string databaseUrl)
    {
        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':', 2);
            var port = uri.Port > 0 ? uri.Port : 5432;
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
            var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

            return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
        }

        return databaseUrl;
    }
}
