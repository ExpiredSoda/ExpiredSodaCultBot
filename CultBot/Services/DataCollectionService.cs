using CultBot.Configuration;
using CultBot.Data;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CultBot.Services;

public class DataCollectionService
{
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;

    public DataCollectionService(IDbContextFactory<CultBotDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task TrackMessageAsync(SocketUserMessage message)
    {
        if (message.Author.IsBot) return;

        var user = message.Author as SocketGuildUser;
        if (user == null) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Log the message
            var messageLog = new UserMessage
            {
                UserId = user.Id,
                GuildId = user.Guild.Id,
                ChannelId = message.Channel.Id,
                Content = message.Content,
                Timestamp = DateTime.UtcNow,
                WasDeleted = false,
                WasFlagged = false
            };
            context.UserMessages.Add(messageLog);

            // Update user activity
            var activity = await context.UserActivities
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.GuildId == user.Guild.Id);

            if (activity == null)
            {
                activity = new UserActivity
                {
                    UserId = user.Id,
                    GuildId = user.Guild.Id,
                    Username = user.Username,
                    FirstSeenAt = DateTime.UtcNow,
                    TotalMessageCount = 1,
                    LastMessageTime = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };
                context.UserActivities.Add(activity);
            }
            else
            {
                activity.TotalMessageCount++;
                activity.LastMessageTime = DateTime.UtcNow;
                activity.Username = user.Username; // Update in case of name change
                activity.LastUpdated = DateTime.UtcNow;
            }

            // Check for game mentions in the message
            await TrackGameMentionsAsync(user, message.Content, context);

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR tracking message: {ex.Message}");
        }
    }

    public async Task TrackUserJoinAsync(SocketGuildUser user)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var activity = await context.UserActivities
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.GuildId == user.Guild.Id);

            if (activity == null)
            {
                activity = new UserActivity
                {
                    UserId = user.Id,
                    GuildId = user.Guild.Id,
                    Username = user.Username,
                    FirstSeenAt = DateTime.UtcNow,
                    JoinedAt = DateTime.UtcNow,
                    JoinCount = 1,
                    LastUpdated = DateTime.UtcNow
                };
                context.UserActivities.Add(activity);
            }
            else
            {
                activity.JoinedAt = DateTime.UtcNow;
                activity.JoinCount++;
                activity.LeftAt = null;
                activity.LastUpdated = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();

            Console.WriteLine($"📊 Tracked join: {user.Username} (Total joins: {activity.JoinCount})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR tracking user join: {ex.Message}");
        }
    }

    public async Task TrackUserLeaveAsync(SocketGuild guild, SocketUser user)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var activity = await context.UserActivities
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.GuildId == guild.Id);

            if (activity != null)
            {
                activity.LeftAt = DateTime.UtcNow;
                activity.LeaveCount++;
                activity.LastUpdated = DateTime.UtcNow;
                await context.SaveChangesAsync();

                Console.WriteLine($"📊 Tracked leave: {user.Username} (Total leaves: {activity.LeaveCount})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR tracking user leave: {ex.Message}");
        }
    }

    public async Task TrackGameActivityAsync(SocketGuildUser user, IActivity? activity)
    {
        try
        {
            if (activity == null || activity.Type != ActivityType.Playing) return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var userActivity = await context.UserActivities
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.GuildId == user.Guild.Id);

            if (userActivity != null)
            {
                // Check if starting a new game
                if (userActivity.CurrentGame != activity.Name)
                {
                    // End previous game session if exists
                    if (!string.IsNullOrEmpty(userActivity.CurrentGame) && userActivity.GameStartedAt.HasValue)
                    {
                        var previousGame = await context.GameActivities
                            .Where(g => g.UserId == user.Id && g.GuildId == user.Guild.Id && 
                                       g.GameName == userActivity.CurrentGame && !g.EndedAt.HasValue)
                            .FirstOrDefaultAsync();

                        if (previousGame != null)
                        {
                            previousGame.EndedAt = DateTime.UtcNow;
                            previousGame.Duration = previousGame.EndedAt - previousGame.StartedAt;
                        }
                    }

                    // Start new game session
                    userActivity.CurrentGame = activity.Name;
                    userActivity.GameStartedAt = DateTime.UtcNow;

                    var newGameActivity = new GameActivity
                    {
                        UserId = user.Id,
                        GuildId = user.Guild.Id,
                        GameName = activity.Name,
                        StartedAt = DateTime.UtcNow
                    };
                    context.GameActivities.Add(newGameActivity);

                    Console.WriteLine($"🎮 {user.Username} started playing: {activity.Name}");
                }

                userActivity.LastUpdated = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR tracking game activity: {ex.Message}");
        }
    }

    public async Task EndGameSessionAsync(SocketGuildUser user)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var userActivity = await context.UserActivities
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.GuildId == user.Guild.Id);

            if (userActivity != null && !string.IsNullOrEmpty(userActivity.CurrentGame))
            {
                var gameSession = await context.GameActivities
                    .Where(g => g.UserId == user.Id && g.GuildId == user.Guild.Id && 
                               g.GameName == userActivity.CurrentGame && !g.EndedAt.HasValue)
                    .FirstOrDefaultAsync();

                if (gameSession != null)
                {
                    gameSession.EndedAt = DateTime.UtcNow;
                    gameSession.Duration = gameSession.EndedAt - gameSession.StartedAt;
                    
                    Console.WriteLine($"🎮 {user.Username} stopped playing: {userActivity.CurrentGame} (Duration: {gameSession.Duration?.TotalMinutes:F1} min)");
                }

                userActivity.CurrentGame = null;
                userActivity.GameStartedAt = null;
                userActivity.LastUpdated = DateTime.UtcNow;

                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR ending game session: {ex.Message}");
        }
    }

    private async Task TrackGameMentionsAsync(SocketGuildUser user, string content, CultBotDbContext context)
    {
        try
        {
            var lowerContent = content.ToLower();

            foreach (var game in BotConfig.TrackedGames)
            {
                if (lowerContent.Contains(game.ToLower()))
                {
                    // Log as a game activity mention (not an active play session)
                    var mention = new GameActivity
                    {
                        UserId = user.Id,
                        GuildId = user.Guild.Id,
                        GameName = $"[Mentioned] {game}",
                        StartedAt = DateTime.UtcNow,
                        EndedAt = DateTime.UtcNow,
                        Duration = TimeSpan.Zero
                    };
                    context.GameActivities.Add(mention);

                    Console.WriteLine($"💬 {user.Username} mentioned game: {game}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR tracking game mentions: {ex.Message}");
        }
    }

    public async Task<Dictionary<string, object>> GetUserStatsAsync(ulong userId, ulong guildId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var activity = await context.UserActivities
                .FirstOrDefaultAsync(a => a.UserId == userId && a.GuildId == guildId);

            if (activity == null)
                return new Dictionary<string, object> { { "error", "User not found" } };

            var messageCount = activity.TotalMessageCount;
            var warningCount = activity.WarningCount;
            var slowModeCount = activity.SlowModeCount;

            var topGames = await context.GameActivities
                .Where(g => g.UserId == userId && g.GuildId == guildId && g.Duration.HasValue)
                .GroupBy(g => g.GameName)
                .Select(g => new
                {
                    Game = g.Key,
                    TotalTime = g.Sum(x => x.Duration!.Value.TotalMinutes)
                })
                .OrderByDescending(x => x.TotalTime)
                .Take(5)
                .ToListAsync();

            return new Dictionary<string, object>
            {
                { "username", activity.Username },
                { "totalMessages", messageCount },
                { "warnings", warningCount },
                { "slowModeApplied", slowModeCount },
                { "joinCount", activity.JoinCount },
                { "leaveCount", activity.LeaveCount },
                { "firstSeen", activity.FirstSeenAt },
                { "currentGame", activity.CurrentGame ?? "None" },
                { "topGames", topGames }
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR getting user stats: {ex.Message}");
            return new Dictionary<string, object> { { "error", ex.Message } };
        }
    }
}
