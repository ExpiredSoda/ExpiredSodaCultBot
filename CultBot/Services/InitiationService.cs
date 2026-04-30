using CultBot.Data;
using Microsoft.EntityFrameworkCore;

namespace CultBot.Services;

public class InitiationService
{
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;

    public InitiationService(IDbContextFactory<CultBotDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<InitiationSession> CreateSessionAsync(ulong userId, ulong guildId, ulong ritualChannelId, ulong ritualMessageId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        var existingPendingSessions = await context.InitiationSessions
            .Where(s => s.UserId == userId && s.GuildId == guildId && s.Status == InitiationSessionStatus.Pending)
            .ToListAsync();

        foreach (var existingSession in existingPendingSessions)
        {
            existingSession.Status = InitiationSessionStatus.Expired;
            existingSession.ExpiredTimeUtc = now;
        }

        var session = new InitiationSession
        {
            UserId = userId,
            GuildId = guildId,
            RitualChannelId = ritualChannelId,
            RitualMessageId = ritualMessageId,
            JoinTimeUtc = now,
            Status = InitiationSessionStatus.Pending
        };

        context.InitiationSessions.Add(session);
        await context.SaveChangesAsync();

        return session;
    }

    public async Task<InitiationSession?> GetPendingSessionAsync(ulong userId, ulong guildId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.InitiationSessions
            .Where(s => s.UserId == userId && s.GuildId == guildId && s.Status == InitiationSessionStatus.Pending)
            .OrderByDescending(s => s.JoinTimeUtc)
            .FirstOrDefaultAsync();
    }

    /// <summary>Get the pending session that owns this ritual message (so only that user can complete it).</summary>
    public async Task<InitiationSession?> GetPendingSessionByRitualMessageAsync(ulong guildId, ulong ritualMessageId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.InitiationSessions
            .Where(s => s.GuildId == guildId && s.RitualMessageId == ritualMessageId && s.Status == InitiationSessionStatus.Pending)
            .FirstOrDefaultAsync();
    }

    public async Task<List<InitiationSession>> GetExpiredSessionsAsync(int timeoutHours)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var cutoffTime = DateTime.UtcNow.AddHours(-timeoutHours);

        return await context.InitiationSessions
            .Where(s => s.Status == InitiationSessionStatus.Pending && s.JoinTimeUtc < cutoffTime)
            .ToListAsync();
    }

    public async Task MarkSessionCompletedAsync(int sessionId, string chosenRole)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var session = await context.InitiationSessions.FindAsync(sessionId);
        if (session != null && session.Status == InitiationSessionStatus.Pending)
        {
            session.Status = InitiationSessionStatus.Completed;
            session.ChosenRole = chosenRole;
            session.CompletedTimeUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkSessionExpiredAsync(int sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var session = await context.InitiationSessions.FindAsync(sessionId);
        if (session != null && session.Status == InitiationSessionStatus.Pending)
        {
            session.Status = InitiationSessionStatus.Expired;
            session.ExpiredTimeUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkReminderSentAsync(int sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var session = await context.InitiationSessions.FindAsync(sessionId);
        if (session != null && session.Status == InitiationSessionStatus.Pending && session.ReminderSentAt == null)
        {
            session.ReminderSentAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }
}
