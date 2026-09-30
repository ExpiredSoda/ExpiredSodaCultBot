using CultBot.Configuration;
using CultBot.Data;

namespace CultBot.Services;

public sealed record InitiationMemberState(
    bool IsBot, bool IsProtected, bool HasHoldingRole, string? PathRole,
    DateTime? JoinedAtUtc, bool CanKick = true);

public interface IInitiationActions
{
    bool IsConfigurationSafe();
    Task<InitiationMemberState?> GetMemberAsync(CancellationToken cancellationToken);
    Task AddPathRoleAsync(string roleKey);
    Task AddHoldingRoleAsync();
    Task RemoveHoldingRoleAsync();
    Task<ulong> SendRitualAsync();
    Task SendWelcomeAsync();
    Task DeleteRitualAsync(ulong messageId);
    Task AnnounceCompletionAsync(string roleKey);
    Task<bool> SendReminderAsync();
    Task KickAsync();
    Task AnnounceExpirationAsync();
}

public enum InitiationOutcome
{
    Skipped, Completed, Created, ReminderSent, Expired, Kicked, ConfigurationDisabled
}

public static class InitiationPolicy
{
    public static bool IsRecentNewcomer(InitiationMemberState member, DateTime now, int maxJoinAgeDays) =>
        maxJoinAgeDays > 0 && member.JoinedAtUtc.HasValue &&
        member.JoinedAtUtc.Value <= now &&
        now - member.JoinedAtUtc.Value <= TimeSpan.FromDays(maxJoinAgeDays);

    public static bool OwnsCurrentMembership(InitiationSession session, InitiationMemberState member, int maxJoinAgeDays) =>
        IsRecentNewcomer(member, session.JoinTimeUtc, maxJoinAgeDays);

    public static bool IsConfigurationSafe(
        bool rolesPresent, bool rolesManageable, bool manageRoles, bool kickMembers,
        bool ritualAccessible, bool distinctRoleIds, int timeoutHours, int graceHours, int recoveryDays) =>
        rolesPresent && rolesManageable && manageRoles && kickMembers && ritualAccessible && distinctRoleIds &&
        timeoutHours > 0 && graceHours > 0 && recoveryDays > 0;
}

/// <summary>Serializes member changes and keeps Discord notifications outside essential state transitions.</summary>
public sealed class InitiationWorkflow
{
    private readonly InitiationService _sessions;
    private readonly TimeProvider _time;

    public InitiationWorkflow(InitiationService sessions, TimeProvider time)
    {
        _sessions = sessions;
        _time = time;
    }

    public Task<InitiationOutcome> CompleteAsync(
        ulong guildId, ulong userId, ulong messageId, string roleKey, IInitiationActions actions,
        CancellationToken cancellationToken = default) =>
        _sessions.RunExclusiveAsync(guildId, userId, async () =>
        {
            var session = await _sessions.GetPendingSessionByRitualMessageAsync(guildId, messageId);
            if (session == null || session.UserId != userId)
                return InitiationOutcome.Skipped;
            if (!actions.IsConfigurationSafe())
                return InitiationOutcome.ConfigurationDisabled;

            var member = await actions.GetMemberAsync(cancellationToken);
            if (!actions.IsConfigurationSafe())
                return InitiationOutcome.ConfigurationDisabled;
            if (member == null || member.IsBot || member.IsProtected ||
                !InitiationPolicy.OwnsCurrentMembership(session, member, BotConfig.RecoveryMaxJoinAgeDays))
                return InitiationOutcome.Skipped;

            // A role may already have been assigned when an earlier database write failed.
            var completedRole = member.PathRole ?? roleKey;
            if (member.PathRole == null)
                await actions.AddPathRoleAsync(completedRole);

            // Persist first. A failed write leaves the ritual intact for a safe retry.
            if (!await _sessions.MarkSessionCompletedAsync(session.Id, completedRole))
                return InitiationOutcome.Skipped;

            await OptionalAsync(actions.RemoveHoldingRoleAsync, "remove holding role");
            await OptionalAsync(() => actions.DeleteRitualAsync(messageId), "delete completed ritual");
            await OptionalAsync(() => actions.AnnounceCompletionAsync(completedRole), "announce completion");
            return InitiationOutcome.Completed;
        }, cancellationToken);

    public Task<InitiationOutcome> RecoverAsync(
        ulong guildId, ulong userId, ulong ritualChannelId, IInitiationActions actions,
        CancellationToken cancellationToken = default) =>
        _sessions.RunExclusiveAsync(guildId, userId, async () =>
        {
            if (!actions.IsConfigurationSafe())
                return InitiationOutcome.ConfigurationDisabled;
            var member = await actions.GetMemberAsync(cancellationToken);
            if (!actions.IsConfigurationSafe())
                return InitiationOutcome.ConfigurationDisabled;
            var now = _time.GetUtcNow().UtcDateTime;
            if (member == null || member.IsBot || member.IsProtected || member.PathRole != null ||
                !InitiationPolicy.IsRecentNewcomer(member, now, BotConfig.RecoveryMaxJoinAgeDays))
                return InitiationOutcome.Skipped;

            var latest = await _sessions.GetLatestSessionAsync(userId, guildId);
            if (latest != null && member.JoinedAtUtc <= latest.JoinTimeUtc)
                return InitiationOutcome.Skipped; // Includes completed/expired memberships; never enroll them again.
            if (latest?.Status == InitiationSessionStatus.Pending)
                await _sessions.MarkSessionExpiredAsync(latest.Id); // A fresh rejoin supersedes the old membership.

            await actions.AddHoldingRoleAsync();
            var messageId = await actions.SendRitualAsync();
            try
            {
                await _sessions.CreateSessionAsync(userId, guildId, ritualChannelId, messageId);
            }
            catch
            {
                await OptionalAsync(() => actions.DeleteRitualAsync(messageId), "delete unrecorded ritual");
                throw;
            }
            await OptionalAsync(actions.SendWelcomeAsync, "send welcome");
            return InitiationOutcome.Created;
        }, cancellationToken);

    public Task<InitiationOutcome> ProcessExpirationAsync(
        InitiationSession candidate, IInitiationActions actions, CancellationToken cancellationToken = default) =>
        _sessions.RunExclusiveAsync(candidate.GuildId, candidate.UserId, async () =>
        {
            // The candidate came from a periodic scan and may already be stale.
            var session = await _sessions.GetSessionAsync(candidate.Id);
            if (session?.Status != InitiationSessionStatus.Pending)
                return InitiationOutcome.Skipped;
            if (!actions.IsConfigurationSafe())
                return InitiationOutcome.ConfigurationDisabled;

            var member = await actions.GetMemberAsync(cancellationToken);
            if (!actions.IsConfigurationSafe())
                return InitiationOutcome.ConfigurationDisabled;
            var safeOutcome = await ReconcileAsync(session, member);
            if (safeOutcome.HasValue)
                return safeOutcome.Value;

            var now = _time.GetUtcNow().UtcDateTime;
            if (now < session.JoinTimeUtc.AddHours(BotConfig.InitiationTimeoutHours))
                return InitiationOutcome.Skipped;
            if (!session.ReminderSentAt.HasValue)
            {
                // An undelivered reminder never starts the grace period.
                if (!await actions.SendReminderAsync())
                    return InitiationOutcome.Skipped;
                await _sessions.MarkReminderSentAsync(session.Id);
                return InitiationOutcome.ReminderSent;
            }
            if (now < session.ReminderSentAt.Value.AddHours(BotConfig.ReminderGracePeriodHours))
                return InitiationOutcome.Skipped;

            // Check fresh Discord roles and authoritative database state directly before the kick.
            member = await actions.GetMemberAsync(cancellationToken);
            session = await _sessions.GetSessionAsync(candidate.Id);
            if (session?.Status != InitiationSessionStatus.Pending || !session.ReminderSentAt.HasValue ||
                _time.GetUtcNow().UtcDateTime < session.ReminderSentAt.Value.AddHours(BotConfig.ReminderGracePeriodHours))
                return InitiationOutcome.Skipped;
            if (!actions.IsConfigurationSafe())
                return InitiationOutcome.ConfigurationDisabled;
            safeOutcome = await ReconcileAsync(session, member);
            if (safeOutcome.HasValue)
                return safeOutcome.Value;
            if (!member!.CanKick)
                return InitiationOutcome.Skipped;

            await actions.KickAsync();
            await _sessions.MarkSessionExpiredAsync(session.Id);
            await OptionalAsync(() => actions.DeleteRitualAsync(session.RitualMessageId), "delete expired ritual");
            await OptionalAsync(actions.AnnounceExpirationAsync, "announce expiration");
            return InitiationOutcome.Kicked;
        }, cancellationToken);

    private async Task<InitiationOutcome?> ReconcileAsync(InitiationSession session, InitiationMemberState? member)
    {
        if (member?.PathRole != null)
        {
            await _sessions.MarkSessionCompletedAsync(session.Id, member.PathRole);
            return InitiationOutcome.Completed;
        }
        if (member == null || member.IsBot || member.IsProtected || !member.HasHoldingRole ||
            !InitiationPolicy.OwnsCurrentMembership(session, member, BotConfig.RecoveryMaxJoinAgeDays))
        {
            await _sessions.MarkSessionExpiredAsync(session.Id);
            return InitiationOutcome.Expired;
        }
        return null;
    }

    private static async Task OptionalAsync(Func<Task> action, string description)
    {
        try { await action(); }
        catch (Exception ex) { Console.WriteLine($"WARNING: Could not {description}: {ex.GetType().Name}"); }
    }
}
