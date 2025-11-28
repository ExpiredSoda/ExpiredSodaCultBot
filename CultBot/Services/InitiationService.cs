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

        var session = new InitiationSession
        {
            UserId = userId,
            GuildId = guildId,
            RitualChannelId = ritualChannelId,
            RitualMessageId = ritualMessageId,
            JoinTimeUtc = DateTime.UtcNow,
            Status = "Pending"
        };

        context.InitiationSessions.Add(session);
        await context.SaveChangesAsync();

        return session;
    }

    public async Task<InitiationSession?> GetPendingSessionAsync(ulong userId, ulong guildId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.InitiationSessions
            .Where(s => s.UserId == userId && s.GuildId == guildId && s.Status == "Pending")
            .OrderByDescending(s => s.JoinTimeUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<List<InitiationSession>> GetExpiredSessionsAsync(int timeoutHours)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var cutoffTime = DateTime.UtcNow.AddHours(-timeoutHours);

        return await context.InitiationSessions
            .Where(s => s.Status == "Pending" && s.JoinTimeUtc < cutoffTime)
            .ToListAsync();
    }

    public async Task MarkSessionCompletedAsync(int sessionId, string chosenRole)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var session = await context.InitiationSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.Status = "Completed";
            session.ChosenRole = chosenRole;
            session.CompletedTimeUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkSessionExpiredAsync(int sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var session = await context.InitiationSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.Status = "Expired";
            session.ExpiredTimeUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }
}
