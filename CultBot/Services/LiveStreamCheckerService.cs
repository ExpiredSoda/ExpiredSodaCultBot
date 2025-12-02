using CultBot.Configuration;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace CultBot.Services;

public class LiveStreamCheckerService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly LiveStreamAnnouncementService _announcementService;
    private readonly TimeSpan _checkInterval;
    private bool _isReady = false;

    public LiveStreamCheckerService(
        DiscordSocketClient client,
        LiveStreamAnnouncementService announcementService)
    {
        _client = client;
        _announcementService = announcementService;
        _checkInterval = TimeSpan.FromMinutes(BotConfig.LiveCheckIntervalMinutes);

        // Subscribe to Ready event
        _client.Ready += OnClientReady;
    }

    private Task OnClientReady()
    {
        _isReady = true;
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for bot to be ready
        while (!_isReady && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        Console.WriteLine($"LiveStreamCheckerService started. Checking every {BotConfig.LiveCheckIntervalMinutes} minutes.");

        // Initial delay to let everything settle
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _announcementService.CheckAndAnnounceAsync(isManualTrigger: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in LiveStreamCheckerService: {ex.Message}");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
