using CultBot.Configuration;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace CultBot.Services;

public class InitiationExpirationService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly InitiationService _initiationService;
    private readonly OnboardingService _onboardingService;
    private readonly IBotReadySignal _readySignal;
    private readonly TimeSpan _checkInterval;

    public InitiationExpirationService(DiscordSocketClient client, InitiationService initiationService, OnboardingService onboardingService, IBotReadySignal readySignal)
    {
        _client = client;
        _initiationService = initiationService;
        _onboardingService = onboardingService;
        _readySignal = readySignal;
        _checkInterval = TimeSpan.FromMinutes(BotConfig.ExpirationCheckIntervalMinutes);
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

        Console.WriteLine("InitiationExpirationService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverMissedInitiationsAsync();
                await CheckExpiredInitiationsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InitiationExpirationService: {ex.Message}");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task RecoverMissedInitiationsAsync()
    {
        foreach (var guild in _client.Guilds)
        {
            var uninitiatedRole = guild.GetRole(BotConfig.TheUninitiatedRoleId);
            var silentWitness = guild.GetRole(BotConfig.SilentWitnessRoleId);
            var neonDisciple = guild.GetRole(BotConfig.NeonDiscipleRoleId);
            var veiledArchivist = guild.GetRole(BotConfig.VeiledArchivistRoleId);
            if (uninitiatedRole == null) continue;

            // Recover: anyone who has no path role (hasn't completed initiation) and no pending session
            foreach (var user in guild.Users.Where(u => !u.IsBot))
            {
                var hasPathRole = (silentWitness != null && user.Roles.Contains(silentWitness)) ||
                    (neonDisciple != null && user.Roles.Contains(neonDisciple)) ||
                    (veiledArchivist != null && user.Roles.Contains(veiledArchivist));
                if (hasPathRole) continue;

                var session = await _initiationService.GetPendingSessionAsync(user.Id, guild.Id);
                if (session != null) continue;

                if (BotConfig.RecoveryMaxJoinAgeDays > 0 && user.JoinedAt.HasValue)
                {
                    var daysSinceJoin = (DateTime.UtcNow - user.JoinedAt.Value.UtcDateTime).TotalDays;
                    if (daysSinceJoin > BotConfig.RecoveryMaxJoinAgeDays)
                        continue;
                }

                try
                {
                    if (!user.Roles.Contains(uninitiatedRole))
                        await user.AddRoleAsync(uninitiatedRole);
                    await _onboardingService.SendGatewayWelcomeForUserAsync(user);
                    await _onboardingService.SendRitualForUninitiatedUserAsync(user);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending recovery ritual to {user.Username}: {ex.Message}");
                }
            }
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
