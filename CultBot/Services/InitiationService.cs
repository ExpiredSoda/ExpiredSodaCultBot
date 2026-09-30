using CultBot.Data;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace CultBot.Services;

public class InitiationService
{
    private static readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), SemaphoreSlim> MemberLocks = new();
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;

    public InitiationService(IDbContextFactory<CultBotDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<T> RunExclusiveAsync<T>(ulong guildId, ulong userId, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        var memberLock = MemberLocks.GetOrAdd((guildId, userId), _ => new SemaphoreSlim(1, 1));
        await memberLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            if (context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // Transaction-scoped advisory locks also serialize rolling deployments/replicas without schema changes.
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"cultbot-initiation:{guildId}:{userId}"));
                var key = BinaryPrimitives.ReadInt64LittleEndian(hash);
                await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
                return await action(); // Disposing the transaction releases the lock, including on failure.
            }
            return await action();
        }
        finally { memberLock.Release(); }
    }

    public async Task<InitiationSession?> GetSessionAsync(int sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.InitiationSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
    }

    public async Task<InitiationSession?> GetLatestSessionAsync(ulong userId, ulong guildId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.InitiationSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.GuildId == guildId)
            .OrderByDescending(s => s.JoinTimeUtc).ThenByDescending(s => s.Id).FirstOrDefaultAsync();
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

    public async Task<bool> MarkSessionCompletedAsync(int sessionId, string chosenRole)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var session = await context.InitiationSessions.FindAsync(sessionId);
        if (session != null && session.Status == InitiationSessionStatus.Pending)
        {
            session.Status = InitiationSessionStatus.Completed;
            session.ChosenRole = chosenRole;
            session.CompletedTimeUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return true;
        }
        return false;
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
