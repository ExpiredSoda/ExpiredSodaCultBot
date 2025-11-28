using CultBot.Configuration;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace CultBot.Services;

public class InitiationExpirationService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly InitiationService _initiationService;
    private readonly TimeSpan _checkInterval;
    private bool _isReady = false;

    public InitiationExpirationService(DiscordSocketClient client, InitiationService initiationService)
    {
        _client = client;
        _initiationService = initiationService;
        _checkInterval = TimeSpan.FromMinutes(BotConfig.ExpirationCheckIntervalMinutes);
        
        // Subscribe to Ready event to know when bot is fully initialized
        _client.Ready += OnClientReady;
    }

    private Task OnClientReady()
    {
        _isReady = true;
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the bot to be fully ready (guilds/channels cached) before starting checks
        while (!_isReady && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        Console.WriteLine("InitiationExpirationService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpiredInitiationsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InitiationExpirationService: {ex.Message}");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckExpiredInitiationsAsync()
    {
        var expiredSessions = await _initiationService.GetExpiredSessionsAsync(BotConfig.InitiationTimeoutHours);

        foreach (var session in expiredSessions)
        {
            try
            {
                var guild = _client.GetGuild(session.GuildId);
                if (guild == null) continue;

                var user = guild.GetUser(session.UserId);
                if (user == null)
                {
                    // User already left, just mark as expired
                    await _initiationService.MarkSessionExpiredAsync(session.Id);
                    continue;
                }

                // Delete the ritual message
                var ritualChannel = guild.GetTextChannel(session.RitualChannelId);
                if (ritualChannel != null)
                {
                    try
                    {
                        var message = await ritualChannel.GetMessageAsync(session.RitualMessageId);
                        if (message != null)
                        {
                            await message.DeleteAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not delete ritual message: {ex.Message}");
                    }

                    // Post failure message
                    var failureMessage = $"{user.Mention} has failed to complete the rites.\n" +
                                       $"They have been cast out of the Cult.";
                    await ritualChannel.SendMessageAsync(failureMessage);
                }

                // Kick the user
                await user.KickAsync("Failed to complete initiation within 24 hours");

                // Mark session as expired
                await _initiationService.MarkSessionExpiredAsync(session.Id);

                Console.WriteLine($"Kicked user {user.Username} (ID: {user.Id}) for failing initiation.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing expired session {session.Id}: {ex.Message}");
            }
        }
    }
}
