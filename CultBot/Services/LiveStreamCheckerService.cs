using CultBot.Configuration;
using CultBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace CultBot.Services;

public class LiveStreamCheckerService : BackgroundService
{
    private readonly LiveStreamAnnouncementService _announcementService;
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;
    private readonly IBotReadySignal _readySignal;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _alreadyLiveCheckInterval;

    public LiveStreamCheckerService(
        LiveStreamAnnouncementService announcementService,
        IDbContextFactory<CultBotDbContext> contextFactory,
        IBotReadySignal readySignal)
    {
        _announcementService = announcementService;
        _contextFactory = contextFactory;
        _readySignal = readySignal;
        _checkInterval = TimeSpan.FromMinutes(BotConfig.LiveCheckIntervalMinutes);
        _alreadyLiveCheckInterval = TimeSpan.FromMinutes(BotConfig.AlreadyLiveCheckIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _readySignal.WaitForReadyAsync(stoppingToken);

        Console.WriteLine($"LiveStreamCheckerService started. Window: {BotConfig.LiveCheckWindowStartHour}:00-{BotConfig.LiveCheckWindowEndHour}:00 {BotConfig.LiveCheckTimezoneId}. Interval: {BotConfig.LiveCheckIntervalMinutes} min (already live: {BotConfig.AlreadyLiveCheckIntervalMinutes} min).");

        // Initial delay to let everything settle
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                TimeSpan delay;
                if (IsWithinLiveCheckWindow())
                {
                    await _announcementService.CheckAndAnnounceAsync(isManualTrigger: false);
                    delay = await GetNextCheckDelayAsync();
                }
                else
                {
                    delay = _checkInterval;
                }

                await Task.Delay(delay, stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in LiveStreamCheckerService: {ex.Message}");
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }

    private static bool IsWithinLiveCheckWindow()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(BotConfig.LiveCheckTimezoneId);
            var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).TimeOfDay;
            var startHour = BotConfig.LiveCheckWindowStartHour;
            var endHour = BotConfig.LiveCheckWindowEndHour;

            if (startHour > endHour)
                return now.TotalHours >= startHour || now.TotalHours < endHour;
            return now.TotalHours >= startHour && now.TotalHours < endHour;
        }
        catch
        {
            return true;
        }
    }

    private async Task<TimeSpan> GetNextCheckDelayAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var status = await context.LiveStreamStatuses
            .FirstOrDefaultAsync(s => s.Platform == BotConfig.LiveStreamPlatformYouTube);

        if (status != null && status.IsLive && status.AnnouncementSent)
            return _alreadyLiveCheckInterval;
        return _checkInterval;
    }
}
