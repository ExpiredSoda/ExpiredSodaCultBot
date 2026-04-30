using CultBot.Configuration;
using Microsoft.Extensions.Hosting;

namespace CultBot.Services;

public class GiveawayBackgroundService : BackgroundService
{
    private readonly GiveawayService _giveawayService;
    private readonly IBotReadySignal _readySignal;
    private readonly TimeSpan _checkInterval;

    public GiveawayBackgroundService(GiveawayService giveawayService, IBotReadySignal readySignal)
    {
        _giveawayService = giveawayService;
        _readySignal = readySignal;
        _checkInterval = TimeSpan.FromMinutes(BotConfig.GiveawayCheckIntervalMinutes);
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

        if (BotConfig.GiveawayChannelId == 0)
        {
            Console.WriteLine("GiveawayBackgroundService: GiveawayChannelId is 0; giveaway checks disabled.");
            return;
        }

        Console.WriteLine($"GiveawayBackgroundService started. Check interval: {BotConfig.GiveawayCheckIntervalMinutes} min.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _giveawayService.CheckAndUpdateGiveawayAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GiveawayBackgroundService: {ex.Message}");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
