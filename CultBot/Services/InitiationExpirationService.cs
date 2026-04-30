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

            // Recover: anyone who has no path role (hasn't completed initiation) and no pending session (skip server owner and admins)
            foreach (var user in guild.Users.Where(u => !u.IsBot))
            {
                if (user.Id == guild.OwnerId || user.GuildPermissions.Administrator)
                    continue;

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

                // Never kick server owner or administrators
                if (user.Id == guild.OwnerId || user.GuildPermissions.Administrator)
                {
                    await _initiationService.MarkSessionExpiredAsync(session.Id);
                    continue;
                }

                var ritualChannel = guild.GetTextChannel(session.RitualChannelId);

                // If we haven't sent the reminder yet, send it and give them ReminderGracePeriodHours (e.g. 24h) more
                if (session.ReminderSentAt == null)
                {
                    var reminderMessage = $"{user.Mention} — the veil grows thin. Your time to choose a path has all but slipped away.\n\n" +
                        $"**Choose your role now**, or your time in this server will come to a close in **{BotConfig.ReminderGracePeriodHours} hours**. The Cult does not wait forever.";
                    var reminderDelivered = false;

                    if (ritualChannel != null)
                    {
                        try
                        {
                            await ritualChannel.SendMessageAsync(reminderMessage);
                            reminderDelivered = true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Could not send expiration reminder: {ex.Message}");
                        }
                    }

                    if (!reminderDelivered)
                    {
                        try
                        {
                            var dmChannel = await user.CreateDMChannelAsync();
                            await dmChannel.SendMessageAsync(reminderMessage);
                            reminderDelivered = true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Could not DM expiration reminder to {user.Username}: {ex.Message}");
                        }
                    }

                    await _initiationService.MarkReminderSentAsync(session.Id);
                    var deliveryStatus = reminderDelivered ? "Sent" : "Could not deliver";
                    Console.WriteLine($"{deliveryStatus} expiration reminder to {user.Username}; grace period of {BotConfig.ReminderGracePeriodHours}h started.");
                    continue;
                }

                // Reminder was sent; check if grace period has passed
                var kickAfter = session.ReminderSentAt.Value.AddHours(BotConfig.ReminderGracePeriodHours);
                if (DateTime.UtcNow < kickAfter)
                    continue;

                // Grace period over — kick
                if (ritualChannel != null)
                {
                    try
                    {
                        var message = await ritualChannel.GetMessageAsync(session.RitualMessageId);
                        if (message != null)
                            await message.DeleteAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not delete ritual message: {ex.Message}");
                    }

                    var failureMessage = $"{user.Mention} has failed to complete the rites.\nThey have been cast out of the Cult.";
                    await ritualChannel.SendMessageAsync(failureMessage);
                }

                await user.KickAsync("Failed to complete initiation within the allowed time");
                await _initiationService.MarkSessionExpiredAsync(session.Id);
                Console.WriteLine($"Kicked user {user.Username} (ID: {user.Id}) for failing initiation after grace period.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing expired session {session.Id}: {ex.Message}");
            }
        }
    }
}
