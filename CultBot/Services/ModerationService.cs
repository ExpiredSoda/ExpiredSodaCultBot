using CultBot.Configuration;
using CultBot.Data;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CultBot.Services;

public class ModerationService
{
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;
    private readonly DiscordSocketClient _client;

    public ModerationService(
        IDbContextFactory<CultBotDbContext> contextFactory,
        DiscordSocketClient client)
    {
        _contextFactory = contextFactory;
        _client = client;
    }

    public async Task WarnUserAsync(SocketGuildUser user, SocketTextChannel? channel, string reason)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Log the warning
            var log = new ModerationLog
            {
                UserId = user.Id,
                GuildId = user.Guild.Id,
                Action = "Warning",
                Reason = reason,
                Timestamp = DateTime.UtcNow,
                IsAutomated = true
            };
            context.ModerationLogs.Add(log);

            // Update user activity
            var activity = await GetOrCreateUserActivityAsync(user, context);
            activity.WarningCount++;
            activity.LastUpdated = DateTime.UtcNow;

            await context.SaveChangesAsync();

            // Send warning message
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("⚠️ Warning")
                    .WithDescription($"{user.Mention}, you have been warned.")
                    .AddField("Reason", reason)
                    .AddField("Total Warnings", activity.WarningCount.ToString())
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
            }

            // Try to DM the user
            try
            {
                var dmChannel = await user.CreateDMChannelAsync();
                await dmChannel.SendMessageAsync($"You received a warning in **{user.Guild.Name}**\n**Reason:** {reason}");
            }
            catch
            {
                // User has DMs disabled
            }

            Console.WriteLine($"⚠️ Warned {user.Username}: {reason}");

            // Log to mod channel
            await LogToModChannelAsync(user.Guild, $"⚠️ **Warning Issued**\nUser: {user.Mention}\nReason: {reason}\nTotal Warnings: {activity.WarningCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR warning user: {ex.Message}");
        }
    }

    public async Task ApplySlowModeAsync(SocketGuildUser user, int durationMinutes)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Update spam tracker - create if doesn't exist
            var tracker = await context.SpamTrackers
                .FirstOrDefaultAsync(t => t.UserId == user.Id && t.GuildId == user.Guild.Id);

            if (tracker == null)
            {
                tracker = new SpamTracker
                {
                    UserId = user.Id,
                    GuildId = user.Guild.Id,
                    RecentMessageTimes = new List<DateTime>(),
                    LastSpamCheck = DateTime.UtcNow
                };
                context.SpamTrackers.Add(tracker);
            }

            tracker.IsSlowModeActive = true;
            tracker.SlowModeUntil = DateTime.UtcNow.AddMinutes(durationMinutes);

            // Log the action
            var log = new ModerationLog
            {
                UserId = user.Id,
                GuildId = user.Guild.Id,
                Action = "SlowMode",
                Reason = $"Slow mode applied for {durationMinutes} minutes",
                Timestamp = DateTime.UtcNow,
                IsAutomated = true
            };
            context.ModerationLogs.Add(log);

            // Update user activity
            var activity = await GetOrCreateUserActivityAsync(user, context);
            activity.SlowModeCount++;
            activity.LastUpdated = DateTime.UtcNow;

            await context.SaveChangesAsync();

            Console.WriteLine($"🐌 Applied slow mode to {user.Username} for {durationMinutes} minutes");

            await LogToModChannelAsync(user.Guild, $"🐌 **Slow Mode Applied**\nUser: {user.Mention}\nDuration: {durationMinutes} minutes");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR applying slow mode: {ex.Message}");
        }
    }

    public async Task<bool> IsUserInSlowModeAsync(ulong userId, ulong guildId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var tracker = await context.SpamTrackers
                .FirstOrDefaultAsync(t => t.UserId == userId && t.GuildId == guildId);

            if (tracker != null && tracker.IsSlowModeActive)
            {
                if (tracker.SlowModeUntil.HasValue && DateTime.UtcNow < tracker.SlowModeUntil.Value)
                {
                    return true;
                }
                else
                {
                    // Slow mode expired, clear it
                    tracker.IsSlowModeActive = false;
                    tracker.SlowModeUntil = null;
                    await context.SaveChangesAsync();
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR checking slow mode: {ex.Message}");
            return false;
        }
    }

    public async Task BanUserAsync(SocketGuildUser user, string reason)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Log the ban
            var log = new ModerationLog
            {
                UserId = user.Id,
                GuildId = user.Guild.Id,
                Action = "Ban",
                Reason = reason,
                Timestamp = DateTime.UtcNow,
                IsAutomated = true
            };
            context.ModerationLogs.Add(log);

            // Update user activity
            var activity = await GetOrCreateUserActivityAsync(user, context);
            activity.IsBanned = true;
            activity.LastUpdated = DateTime.UtcNow;

            await context.SaveChangesAsync();

            // Execute the ban
            await user.BanAsync(0, reason);

            Console.WriteLine($"🔨 Banned {user.Username}: {reason}");

            await LogToModChannelAsync(user.Guild, $"🔨 **User Banned**\nUser: {user.Mention} ({user.Username}#{user.Discriminator})\nReason: {reason}\nAction: Automated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR banning user: {ex.Message}");
        }
    }

    private async Task<UserActivity> GetOrCreateUserActivityAsync(SocketGuildUser user, CultBotDbContext context)
    {
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
                LastUpdated = DateTime.UtcNow
            };
            context.UserActivities.Add(activity);
        }

        return activity;
    }

    private async Task LogToModChannelAsync(SocketGuild guild, string message)
    {
        try
        {
            if (BotConfig.ModLogChannelId == 0) return;

            var modChannel = guild.GetTextChannel(BotConfig.ModLogChannelId);
            if (modChannel != null)
            {
                await modChannel.SendMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR logging to mod channel: {ex.Message}");
        }
    }
}
