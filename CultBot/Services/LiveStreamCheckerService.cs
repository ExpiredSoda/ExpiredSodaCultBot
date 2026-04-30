using CultBot.Configuration;
using Microsoft.Extensions.Hosting;

namespace CultBot.Services;

public class LiveStreamCheckerService : BackgroundService
{
    private readonly LiveStreamAnnouncementService _announcementService;
    private readonly IBotReadySignal _readySignal;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _alreadyLiveCheckInterval;

    public LiveStreamCheckerService(
        LiveStreamAnnouncementService announcementService,
        IBotReadySignal readySignal)
    {
        _announcementService = announcementService;
        _readySignal = readySignal;
        _checkInterval = TimeSpan.FromMinutes(BotConfig.LiveCheckIntervalMinutes);
        _alreadyLiveCheckInterval = TimeSpan.FromMinutes(BotConfig.AlreadyLiveCheckIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _readySignal.WaitForReadyAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Console.WriteLine($"LiveStreamCheckerService started. Checks run all day. Interval: {BotConfig.LiveCheckIntervalMinutes} min (already live: {BotConfig.AlreadyLiveCheckIntervalMinutes} min).");

        // Initial delay to let everything settle
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _announcementService.CheckAndAnnounceAsync(isManualTrigger: false);
                var delay = result.IsLive ? _alreadyLiveCheckInterval : _checkInterval;

                await Task.Delay(delay, stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in LiveStreamCheckerService: {ex.Message}");
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}
