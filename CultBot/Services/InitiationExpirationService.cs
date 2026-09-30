using CultBot.Configuration;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace CultBot.Services;

public class InitiationExpirationService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly InitiationService _sessions;
    private readonly InitiationWorkflow _workflow;
    private readonly OnboardingService _onboarding;
    private readonly ConfigurationValidator _validator;
    private readonly IBotReadySignal _readySignal;

    public InitiationExpirationService(DiscordSocketClient client, InitiationService sessions,
        InitiationWorkflow workflow, OnboardingService onboarding, ConfigurationValidator validator,
        IBotReadySignal readySignal)
    {
        _client = client;
        _sessions = sessions;
        _workflow = workflow;
        _onboarding = onboarding;
        _validator = validator;
        _readySignal = readySignal;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _readySignal.WaitForReadyAsync(stoppingToken);
            Console.WriteLine("InitiationExpirationService started with member safety checks.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var guild in _client.Guilds)
                    {
                        if (!_validator.IsInitiationConfigurationSafe(guild, out var reason))
                        {
                            Console.WriteLine($"Initiation actions disabled for guild {guild.Id}: {reason}");
                            continue;
                        }
                        var now = DateTime.UtcNow;
                        var candidates = guild.Users.Where(user => !user.IsBot && user.JoinedAt.HasValue &&
                            now - user.JoinedAt.Value.UtcDateTime <= TimeSpan.FromDays(BotConfig.RecoveryMaxJoinAgeDays) &&
                            !user.Roles.Any(role => role.Id == BotConfig.SilentWitnessRoleId ||
                                role.Id == BotConfig.NeonDiscipleRoleId || role.Id == BotConfig.VeiledArchivistRoleId));
                        foreach (var user in candidates)
                        {
                            try
                            {
                                var outcome = await _workflow.RecoverAsync(guild.Id, user.Id, BotConfig.RoleRitualChannelId,
                                    _onboarding.CreateActions(guild, user.Id), stoppingToken);
                                if (outcome == InitiationOutcome.Created) Console.WriteLine("Recovered newcomer initiation.");
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            { Console.WriteLine($"Could not recover initiation: {ex.GetType().Name}"); }
                        }
                    }
                    foreach (var candidate in await _sessions.GetExpiredSessionsAsync(BotConfig.InitiationTimeoutHours))
                    {
                        var guild = _client.GetGuild(candidate.GuildId);
                        if (guild == null) continue;
                        try
                        {
                            var outcome = await _workflow.ProcessExpirationAsync(candidate,
                                _onboarding.CreateActions(guild, candidate.UserId, candidate.RitualChannelId), stoppingToken);
                            if (outcome != InitiationOutcome.Skipped) Console.WriteLine($"Initiation expiration check: {outcome}");
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        { Console.WriteLine($"Could not process initiation expiration: {ex.GetType().Name}"); }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { Console.WriteLine($"Initiation expiration scan failed: {ex.GetType().Name}"); }
                await Task.Delay(TimeSpan.FromMinutes(BotConfig.ExpirationCheckIntervalMinutes), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
