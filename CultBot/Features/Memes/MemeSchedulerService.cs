using CultBot.Configuration;
using CultBot.Services;
using Microsoft.Extensions.Hosting;

namespace CultBot.Features.Memes;

public class MemeSchedulerService : BackgroundService
{
    private readonly MemePostingService _memePostingService;
    private readonly IBotReadySignal _readySignal;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    public MemeSchedulerService(
        MemePostingService memePostingService,
        IBotReadySignal readySignal)
    {
        _memePostingService = memePostingService;
        _readySignal = readySignal;
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

        if (!_memePostingService.IsEnabled(out var disabledReason))
        {
            Console.WriteLine($"MemeSchedulerService disabled: {disabledReason}.");
            return;
        }

        Console.WriteLine(
            $"MemeSchedulerService started. Source: Tumblr tags. Schedule: 9:00 AM, 2:00 PM, 8:00 PM America/New_York.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dueSlots = MemeSchedule.GetDueSlots(DateTime.UtcNow);
                foreach (var slot in dueSlots)
                {
                    var result = await _memePostingService.PostScheduledMemeAsync(slot, stoppingToken);
                    if (result.Success || !result.Skipped)
                    {
                        Console.WriteLine(result.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in MemeSchedulerService: {ex.Message}");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
