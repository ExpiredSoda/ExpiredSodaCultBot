using CultBot.Configuration;
using CultBot.Data;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CultBot.Services;

public class SpamDetectionService
{
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;
    private readonly ModerationService _moderationService;

    public SpamDetectionService(
        IDbContextFactory<CultBotDbContext> contextFactory,
        ModerationService moderationService)
    {
        _contextFactory = contextFactory;
        _moderationService = moderationService;
    }

    public async Task<bool> CheckForSpamAsync(SocketUserMessage message)
    {
        if (message.Author.IsBot) return false;

        var user = message.Author as SocketGuildUser;
        if (user == null) return false;

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get or create spam tracker
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

        // Clean old messages outside time window
        var cutoffTime = DateTime.UtcNow.AddSeconds(-BotConfig.SpamTimeWindowSeconds);
        tracker.RecentMessageTimes.RemoveAll(t => t < cutoffTime);

        // Add current message
        tracker.RecentMessageTimes.Add(DateTime.UtcNow);

        // Calculate spam score
        int spamScore = 0;

        // Check message frequency
        if (tracker.RecentMessageTimes.Count >= BotConfig.SpamMessageThreshold)
        {
            spamScore += 5;
            Console.WriteLine($"⚠️ Spam detected: {user.Username} sent {tracker.RecentMessageTimes.Count} messages in {BotConfig.SpamTimeWindowSeconds}s");
        }

        // Check for repeated content
        var recentMessages = await context.UserMessages
            .Where(m => m.UserId == user.Id && m.GuildId == user.Guild.Id)
            .OrderByDescending(m => m.Timestamp)
            .Take(5)
            .ToListAsync();

        if (recentMessages.Count >= 3)
        {
            var distinctContent = recentMessages.Select(m => m.Content.ToLower().Trim()).Distinct().Count();
            if (distinctContent == 1)
            {
                spamScore += 10;
                Console.WriteLine($"⚠️ Repeated content detected from {user.Username}");
            }
        }

        // Check for excessive links
        int linkCount = Regex.Matches(message.Content, @"https?://").Count;
        if (linkCount >= 2)
        {
            spamScore += linkCount * 3;
            Console.WriteLine($"⚠️ Multiple links detected from {user.Username}: {linkCount} links");
        }

        // Check for gibberish (high ratio of non-alphanumeric chars)
        if (message.Content.Length > 10)
        {
            int alphanumeric = message.Content.Count(char.IsLetterOrDigit);
            double ratio = (double)alphanumeric / message.Content.Length;
            if (ratio < 0.4)
            {
                spamScore += 5;
                Console.WriteLine($"⚠️ Gibberish detected from {user.Username}");
            }
        }

        // Check for extremely long messages
        if (message.Content.Length > BotConfig.BotMessageLengthThreshold)
        {
            spamScore += 3;
        }

        tracker.SpamScore = spamScore;
        tracker.LastSpamCheck = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // Take action based on score
        if (spamScore >= BotConfig.BotBanThreshold)
        {
            // Check if likely a bot
            bool isLikelyBot = await IsSuspectedBotAsync(user, context);
            if (isLikelyBot)
            {
                await _moderationService.BanUserAsync(user, "Automated ban: Bot spam detected");
                Console.WriteLine($"🔨 Auto-banned suspected bot: {user.Username}");
                return true;
            }
        }

        if (spamScore >= BotConfig.SpamScoreThreshold)
        {
            await _moderationService.ApplySlowModeAsync(user, BotConfig.SlowModeDurationMinutes);
            await _moderationService.WarnUserAsync(user, message.Channel as SocketTextChannel, 
                "Spam detected. Slow mode applied. Please avoid rapid/repeated messages.");
            return true;
        }

        return false;
    }

    private async Task<bool> IsSuspectedBotAsync(SocketGuildUser user, CultBotDbContext context)
    {
        int botScore = 0;

        // Check account age
        var accountAge = DateTime.UtcNow - user.CreatedAt.UtcDateTime;
        if (accountAge.TotalDays < BotConfig.BotAccountAgeThresholdDays)
        {
            botScore += 5;
            Console.WriteLine($"  Bot check: {user.Username} account is {accountAge.TotalDays:F1} days old");
        }

        // Check message history for bot patterns
        var messageCount = await context.UserMessages
            .Where(m => m.UserId == user.Id && m.GuildId == user.Guild.Id)
            .CountAsync();

        var messagesWithLinks = await context.UserMessages
            .Where(m => m.UserId == user.Id && m.GuildId == user.Guild.Id && m.Content.Contains("http"))
            .CountAsync();

        if (messageCount > 0)
        {
            double linkRatio = (double)messagesWithLinks / messageCount;
            if (linkRatio > 0.5) // More than 50% of messages have links
            {
                botScore += 5;
                Console.WriteLine($"  Bot check: {user.Username} has {linkRatio:P0} messages with links");
            }
        }

        // Check if they have a default avatar
        if (user.GetAvatarUrl() == user.GetDefaultAvatarUrl())
        {
            botScore += 2;
        }

        // Check if username matches bot patterns
        if (Regex.IsMatch(user.Username, @"\d{4,}") || // Many numbers
            user.Username.Length > 25 || // Very long username
            !Regex.IsMatch(user.Username, @"[a-zA-Z]")) // No letters
        {
            botScore += 3;
            Console.WriteLine($"  Bot check: {user.Username} has suspicious username pattern");
        }

        Console.WriteLine($"  Bot score for {user.Username}: {botScore}");
        return botScore >= 10;
    }
}
