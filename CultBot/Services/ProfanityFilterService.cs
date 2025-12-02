using CultBot.Configuration;
using CultBot.Data;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CultBot.Services;

public class ProfanityFilterService
{
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;
    private readonly ModerationService _moderationService;

    public ProfanityFilterService(
        IDbContextFactory<CultBotDbContext> contextFactory,
        ModerationService moderationService)
    {
        _contextFactory = contextFactory;
        _moderationService = moderationService;
    }

    public async Task<bool> CheckMessageForProfanityAsync(SocketUserMessage message)
    {
        if (message.Author.IsBot) return false;

        var user = message.Author as SocketGuildUser;
        if (user == null) return false;

        var content = message.Content.ToLower();

        // Check for racial slurs
        foreach (var slur in BotConfig.RacialSlurs)
        {
            if (string.IsNullOrWhiteSpace(slur)) continue;

            // Word boundary matching to avoid false positives
            var pattern = $@"\b{Regex.Escape(slur.ToLower())}\b";
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                await HandleProfanityDetectionAsync(user, message, slur, "Racial slur");
                return true;
            }

            // Check for variations with special characters (e.g., "w0rd" instead of "word")
            var obfuscatedPattern = CreateObfuscatedPattern(slur);
            if (Regex.IsMatch(content, obfuscatedPattern, RegexOptions.IgnoreCase))
            {
                await HandleProfanityDetectionAsync(user, message, slur, "Racial slur (obfuscated)");
                return true;
            }
        }

        return false;
    }

    private string CreateObfuscatedPattern(string word)
    {
        // Create a pattern that matches common obfuscation techniques
        var pattern = word.ToLower();
        pattern = pattern.Replace("a", "[a@4]");
        pattern = pattern.Replace("e", "[e3]");
        pattern = pattern.Replace("i", "[i1!]");
        pattern = pattern.Replace("o", "[o0]");
        pattern = pattern.Replace("s", "[s5$]");
        pattern = pattern.Replace("t", "[t7]");
        
        // Allow spaces, dashes, or underscores between characters
        pattern = Regex.Replace(pattern, @"(?<=.)", @"[\s\-_]*");
        
        return $@"\b{pattern}\b";
    }

    private async Task HandleProfanityDetectionAsync(
        SocketGuildUser user, 
        SocketUserMessage message, 
        string detectedTerm, 
        string category)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Delete the message immediately
            try
            {
                await message.DeleteAsync();
                Console.WriteLine($"🗑️ Deleted message from {user.Username} containing: {category}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR deleting message: {ex.Message}");
            }

            // Log the violation
            var messageLog = new UserMessage
            {
                UserId = user.Id,
                GuildId = user.Guild.Id,
                ChannelId = message.Channel.Id,
                Content = message.Content,
                Timestamp = DateTime.UtcNow,
                WasDeleted = true,
                WasFlagged = true,
                FlagReason = $"{category}: {detectedTerm}"
            };
            context.UserMessages.Add(messageLog);

            var moderationLog = new ModerationLog
            {
                UserId = user.Id,
                GuildId = user.Guild.Id,
                Action = "MessageDeleted",
                Reason = $"{category} detected in message",
                Timestamp = DateTime.UtcNow,
                IsAutomated = true
            };
            context.ModerationLogs.Add(moderationLog);

            // Get offense count
            var offenseCount = await context.ModerationLogs
                .Where(m => m.UserId == user.Id && m.GuildId == user.Guild.Id && 
                           m.Action == "MessageDeleted" && m.Reason.Contains(category))
                .CountAsync();

            await context.SaveChangesAsync();

            // Escalate based on offense count
            if (offenseCount == 1)
            {
                // First offense: Warning
                await _moderationService.WarnUserAsync(user, message.Channel as SocketTextChannel, 
                    $"Use of inappropriate language ({category}) is not allowed.");
            }
            else if (offenseCount == 2)
            {
                // Second offense: Final warning + slow mode
                await _moderationService.WarnUserAsync(user, message.Channel as SocketTextChannel, 
                    $"Final warning: Repeated use of inappropriate language. Slow mode applied.");
                await _moderationService.ApplySlowModeAsync(user, 30); // 30 minutes
            }
            else if (offenseCount >= 3)
            {
                // Third offense: Ban
                await _moderationService.BanUserAsync(user, 
                    $"Banned for repeated violations: {category}");
            }

            // Send ephemeral notice in channel
            var channel = message.Channel as SocketTextChannel;
            if (channel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("🚫 Message Removed")
                    .WithDescription($"A message from {user.Mention} was removed for violating server rules.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                var sentMessage = await channel.SendMessageAsync(embed: embed);
                
                // Delete the notice after 10 seconds
                _ = Task.Delay(10000).ContinueWith(_ => sentMessage.DeleteAsync());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR handling profanity detection: {ex.Message}");
        }
    }
}
