using CultBot.Configuration;
using CultBot.Data;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace CultBot.Services;

public class GiveawayService
{
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> DrawLocks = new();

    private readonly DiscordSocketClient _client;
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;

    public GiveawayService(DiscordSocketClient client, IDbContextFactory<CultBotDbContext> contextFactory)
    {
        _client = client;
        _contextFactory = contextFactory;
    }

    /// <summary>Check all guilds with a configured giveaway channel: update progress (only when count changes or weekly), or post draw button when goal reached.</summary>
    public async Task CheckAndUpdateGiveawayAsync()
    {
        if (BotConfig.GiveawayChannelId == 0)
            return;

        foreach (var guild in _client.Guilds)
        {
            try
            {
                await CheckAndUpdateGiveawayForGuildAsync(guild);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GiveawayService error for guild {guild.Name}: {ex.Message}");
            }
        }
    }

    private async Task CheckAndUpdateGiveawayForGuildAsync(SocketGuild guild)
    {
        var channel = guild.GetTextChannel(BotConfig.GiveawayChannelId);
        if (channel == null)
            return;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var state = await GetOrCreateStateAsync(context, guild.Id);

        var eligible = GetEligibleMembers(guild);
        var count = eligible.Count;

        // Goal reached: post "Draw winners" button (only host can press); don't auto-run
        if (count >= state.CurrentGoal)
        {
            await EnsureDrawButtonMessageAsync(guild, channel, state, count, context);
            await context.SaveChangesAsync();
            return;
        }

        await UpdateProgressMessageAsync(guild, channel, state, count, context);
        await context.SaveChangesAsync();
    }

    private static List<SocketGuildUser> GetEligibleMembers(SocketGuild guild)
    {
        var silentWitness = guild.GetRole(BotConfig.SilentWitnessRoleId);
        var neonDisciple = guild.GetRole(BotConfig.NeonDiscipleRoleId);
        var veiledArchivist = guild.GetRole(BotConfig.VeiledArchivistRoleId);

        return guild.Users
            .Where(u => !u.IsBot)
            .Where(u =>
                (silentWitness != null && u.Roles.Contains(silentWitness)) ||
                (neonDisciple != null && u.Roles.Contains(neonDisciple)) ||
                (veiledArchivist != null && u.Roles.Contains(veiledArchivist)))
            .ToList();
    }

    private static async Task<GiveawayState> GetOrCreateStateAsync(CultBotDbContext context, ulong guildId)
    {
        var state = await context.GiveawayStates.FirstOrDefaultAsync(s => s.GuildId == guildId);
        if (state != null)
            return state;

        state = new GiveawayState
        {
            GuildId = guildId,
            CurrentGoal = BotConfig.MemberGoal
        };
        context.GiveawayStates.Add(state);
        await context.SaveChangesAsync();
        return state;
    }

    private async Task UpdateProgressMessageAsync(SocketGuild guild, SocketTextChannel channel, GiveawayState state, int count, CultBotDbContext context)
    {
        var now = DateTime.UtcNow;
        var needWeeklyUpdate = state.LastWeeklyUpdateAt == null ||
            (now - state.LastWeeklyUpdateAt.Value).TotalDays >= BotConfig.WeeklyUpdateIntervalDays;

        // Weekly update: always send a new message once per week
        if (needWeeklyUpdate)
        {
            var weeklyText = $"📊 **Weekly giveaway update** — **{count}** / **{state.CurrentGoal}** initiated members. " +
                "When we hit the goal, the host will run the draw. Prizes: " +
                $"**{BotConfig.GiveawayPrize1}**, **{BotConfig.GiveawayPrize2}**, **{BotConfig.GiveawayPrize3}**.";
            await channel.SendMessageAsync(weeklyText);
            state.LastWeeklyUpdateAt = now;
        }

        // Progress message: only update when count changed (don't repeat same count)
        if (state.LastAnnouncedCount == count)
            return;

        var text = $"🎁 **Giveaway progress** — **{count}** / **{state.CurrentGoal}** initiated members. " +
                   "When we hit the goal, the host will run the draw. Prizes: " +
                   $"**{BotConfig.GiveawayPrize1}**, **{BotConfig.GiveawayPrize2}**, **{BotConfig.GiveawayPrize3}**.";

        if (state.ProgressMessageId != null)
        {
            try
            {
                var message = await channel.GetMessageAsync(state.ProgressMessageId.Value) as IUserMessage;
                if (message != null)
                {
                    await message.ModifyAsync(m => m.Content = text);
                    state.LastAnnouncedCount = count;
                    return;
                }
            }
            catch
            {
                // Message deleted or inaccessible; fall through to send new
            }
        }

        var newMessage = await channel.SendMessageAsync(text);
        state.ProgressMessageId = newMessage.Id;
        state.LastAnnouncedCount = count;
    }

    /// <summary>When goal is reached, ensure there is a message with "Draw winners" button (only host can press).</summary>
    private async Task EnsureDrawButtonMessageAsync(SocketGuild guild, SocketTextChannel channel, GiveawayState state, int count, CultBotDbContext context)
    {
        if (state.PendingDrawMessageId != null)
        {
            try
            {
                var existing = await channel.GetMessageAsync(state.PendingDrawMessageId.Value);
                if (existing != null)
                    return; // Button message already there
            }
            catch
            {
                // Message gone; post new one
            }
        }

        var hostLabel = BotConfig.GiveawayHostUserId != 0
            ? $"<@{BotConfig.GiveawayHostUserId}>"
            : BotConfig.GiveawayHostUsername;

        var builder = new ComponentBuilder()
            .WithButton("Draw winners", BotConfig.GiveawayDrawButtonCustomId, ButtonStyle.Primary);

        var msg = await channel.SendMessageAsync(
            $"🎉 **Goal reached!** **{count}** / **{state.CurrentGoal}** initiated members.\n\n" +
            $"Only **{hostLabel}** can press the button below to run the draw. " +
            "Three random initiated members will win 1st, 2nd, and 3rd place.",
            components: builder.Build());

        state.PendingDrawMessageId = msg.Id;
    }

    /// <summary>Handle the draw button press: only host can run; cycles through members then picks 3 winners. Returns true if this was a giveaway button.</summary>
    public async Task<bool> HandleDrawButtonAsync(SocketMessageComponent interaction)
    {
        if (interaction.Data.CustomId != BotConfig.GiveawayDrawButtonCustomId)
            return false;

        var user = interaction.User;
        if (!IsGiveawayHost(user))
        {
            await interaction.RespondAsync($"Only the host (**{BotConfig.GiveawayHostUsername}**) can run the draw.", ephemeral: true);
            return true;
        }

        await interaction.DeferAsync(ephemeral: true);

        var channel = interaction.Channel as SocketTextChannel;
        var guild = (interaction.User as SocketGuildUser)?.Guild;
        if (channel == null || guild == null)
        {
            await interaction.FollowupAsync("Could not resolve channel or guild.", ephemeral: true);
            return true;
        }

        var drawLock = DrawLocks.GetOrAdd(guild.Id, _ => new SemaphoreSlim(1, 1));
        await drawLock.WaitAsync();

        try
        {
            var eligible = GetEligibleMembers(guild);
            if (eligible.Count < 3)
            {
                await interaction.FollowupAsync("Not enough initiated members (need at least 3) to run the draw.", ephemeral: true);
                return true;
            }

            await using var context = await _contextFactory.CreateDbContextAsync();
            var state = await context.GiveawayStates.FirstOrDefaultAsync(s => s.GuildId == guild.Id);
            if (state == null || state.PendingDrawMessageId != interaction.Message.Id)
            {
                await interaction.FollowupAsync("This draw is no longer valid.", ephemeral: true);
                return true;
            }

            var message = await channel.GetMessageAsync(interaction.Message.Id) as IUserMessage;
            if (message == null)
            {
                await interaction.FollowupAsync("Could not find the message.", ephemeral: true);
                return true;
            }

            await message.ModifyAsync(m =>
            {
                m.Content = "🎲 Giveaway draw in progress...";
                m.Components = null;
            });

            // Cycle: update message with random members for a few seconds.
            var cycleEnd = DateTime.UtcNow.AddSeconds(BotConfig.GiveawayCycleDurationSeconds);
            var cycleDelayMs = Math.Max(BotConfig.GiveawayCycleUpdateIntervalMs, 1000);

            while (DateTime.UtcNow < cycleEnd)
            {
                var member = eligible[Random.Shared.Next(eligible.Count)];
                await message.ModifyAsync(m =>
                {
                    m.Content = $"🎲 Considering... {member.Mention}";
                    m.Components = null;
                });
                await Task.Delay(cycleDelayMs);
            }

            // Pick 3 distinct winners (1st, 2nd, 3rd)
            var shuffled = eligible.OrderBy(_ => Guid.NewGuid()).Take(3).ToList();
            var winner1 = shuffled[0];
            var winner2 = shuffled[1];
            var winner3 = shuffled[2];
            var completedGoal = state.CurrentGoal;
            var nextGoal = GetNextGoal(completedGoal);

            var announcement =
                "🎉 **Giveaway draw complete!** 🎉\n\n" +
                $"**{completedGoal}** initiated members — the Cult has spoken.\n\n" +
                $"🥇 **1st** — {BotConfig.GiveawayPrize1} — {winner1.Mention}\n" +
                $"🥈 **2nd** — {BotConfig.GiveawayPrize2} — {winner2.Mention}\n" +
                $"🥉 **3rd** — {BotConfig.GiveawayPrize3} — {winner3.Mention}\n\n" +
                $"Winners: reach out to staff to claim your prize. Next goal: **{nextGoal}** members!";

            await message.ModifyAsync(m =>
            {
                m.Content = announcement;
                m.Components = null;
            });

            state.LastGiveawayGoalReached = completedGoal;
            state.CurrentGoal = nextGoal;
            state.PendingDrawMessageId = null;
            state.ProgressMessageId = null;
            state.LastAnnouncedCount = null;
            await context.SaveChangesAsync();

            await interaction.FollowupAsync("Draw complete! Winners announced above.", ephemeral: true);
            Console.WriteLine($"Giveaway completed for guild {guild.Name}: goal {state.LastGiveawayGoalReached} reached.");
        }
        finally
        {
            drawLock.Release();
        }

        return true;
    }

    private static bool IsGiveawayHost(SocketUser user)
    {
        if (BotConfig.GiveawayHostUserId != 0)
            return user.Id == BotConfig.GiveawayHostUserId;

        return string.Equals(user.Username, BotConfig.GiveawayHostUsername, StringComparison.OrdinalIgnoreCase) ||
            (user is SocketGuildUser guildUser &&
                string.Equals(guildUser.GlobalName, BotConfig.GiveawayHostUsername, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetNextGoal(int completedGoal)
    {
        if (completedGoal < BotConfig.NextGoalAfterGiveaway)
            return BotConfig.NextGoalAfterGiveaway;

        var increment = Math.Max(BotConfig.MemberGoal, BotConfig.NextGoalAfterGiveaway - BotConfig.MemberGoal);
        return completedGoal + increment;
    }
}
