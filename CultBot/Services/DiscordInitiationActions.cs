using CultBot.Configuration;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System.Net;

namespace CultBot.Services;

public sealed class DiscordInitiationActions : IInitiationActions
{
    private readonly DiscordSocketClient _client;
    private readonly SocketGuild _guild;
    private readonly ulong _userId;
    private readonly ulong _ritualChannelId;
    private readonly ConfigurationValidator _validator;
    private RestGuildUser? _member;
    private bool _freshConfigurationSafe = true;

    public DiscordInitiationActions(DiscordSocketClient client, SocketGuild guild, ulong userId,
        ulong ritualChannelId, ConfigurationValidator validator)
    {
        _client = client;
        _guild = guild;
        _userId = userId;
        _ritualChannelId = ritualChannelId;
        _validator = validator;
    }

    public bool IsConfigurationSafe()
    {
        var safe = _validator.IsInitiationConfigurationSafe(_guild, out var reason) && _freshConfigurationSafe;
        if (!_freshConfigurationSafe) reason = "current Discord roles or bot permissions are unsafe";
        if (!safe) Console.WriteLine($"Initiation actions disabled for guild {_guild.Id}: {reason}");
        return safe;
    }

    public async Task<InitiationMemberState?> GetMemberAsync(CancellationToken cancellationToken)
    {
        // Bypass the gateway cache. Only a real 404 means the member has left; other failures abort safely.
        try
        {
            _member = await _client.Rest.GetGuildUserAsync(_guild.Id, _userId,
                new RequestOptions { CancelToken = cancellationToken });
        }
        catch (Discord.Net.HttpException ex) when (ex.HttpCode == HttpStatusCode.NotFound)
        { _member = null; return null; }
        if (_member == null) return null;

        // Fetch role definitions and the bot's own roles too, so hierarchy/Admin checks do not trust stale gateway data.
        var options = new RequestOptions { CancelToken = cancellationToken };
        var guild = await _client.Rest.GetGuildAsync(_guild.Id, options);
        var bot = await _client.Rest.GetGuildUserAsync(_guild.Id, _client.CurrentUser.Id, options);
        var roles = _member.RoleIds.Select(guild.GetRole).Append(guild.GetRole(_guild.Id)).ToList();
        var botRoles = bot.RoleIds.Select(guild.GetRole).Append(guild.GetRole(_guild.Id)).ToList();
        var botPosition = botRoles.Where(role => role != null).Select(role => role!.Position).DefaultIfEmpty(-1).Max();
        var configuredRoles = new[] { BotConfig.TheUninitiatedRoleId, BotConfig.SilentWitnessRoleId,
            BotConfig.NeonDiscipleRoleId, BotConfig.VeiledArchivistRoleId }.Select(guild.GetRole).ToArray();
        _freshConfigurationSafe = botRoles.All(role => role != null) &&
            configuredRoles.All(role => role != null && !role.IsManaged && !role.Permissions.Administrator && role.Position < botPosition) &&
            botRoles.Any(role => role!.Permissions.ManageRoles || role.Permissions.Administrator) &&
            botRoles.Any(role => role!.Permissions.KickMembers || role.Permissions.Administrator);
        var memberPosition = roles.Where(role => role != null).Select(role => role!.Position).DefaultIfEmpty(0).Max();
        var isProtected = _member.Id == guild.OwnerId || roles.Any(role => role == null || role.Permissions.Administrator);
        var path = _member.RoleIds.Contains(BotConfig.SilentWitnessRoleId) ? "SilentWitness" :
            _member.RoleIds.Contains(BotConfig.NeonDiscipleRoleId) ? "NeonDisciple" :
            _member.RoleIds.Contains(BotConfig.VeiledArchivistRoleId) ? "VeiledArchivist" : null;
        return new InitiationMemberState(_member.IsBot, isProtected,
            _member.RoleIds.Contains(BotConfig.TheUninitiatedRoleId), path, _member.JoinedAt?.UtcDateTime,
            roles.All(role => role != null) && botPosition > memberPosition);
    }

    public Task AddPathRoleAsync(string key) => Member.AddRoleAsync(GetRole(key).Id);
    public Task AddHoldingRoleAsync() => Member.AddRoleAsync(BotConfig.TheUninitiatedRoleId);
    public Task RemoveHoldingRoleAsync() => Member.RemoveRoleAsync(BotConfig.TheUninitiatedRoleId);

    public async Task<ulong> SendRitualAsync()
    {
        var components = new ComponentBuilder()
            .WithButton("Become a Silent Witness", BotConfig.ButtonSilentWitness, ButtonStyle.Secondary)
            .WithButton("Become a Neon Disciple", BotConfig.ButtonNeonDisciple, ButtonStyle.Secondary)
            .WithButton("Become a Veiled Archivist", BotConfig.ButtonVeiledArchivist, ButtonStyle.Secondary).Build();
        var text = $"<@{_userId}>, choose your path to enter the Cult.\n\n" +
            "**Silent Witness** - for those who watch from the shadows.\n" +
            "**Neon Disciple** - for those who challenge themselves in digital arenas.\n" +
            "**Veiled Archivist** - for those who seek stories, lore, and horror.\n\n" +
            $"Select one below.\nYou have **{BotConfig.InitiationTimeoutHours} hours**.";
        return (await RitualChannel.SendMessageAsync(text, components: components)).Id;
    }

    public async Task SendWelcomeAsync()
    {
        var channel = _guild.GetTextChannel(BotConfig.GatewayChannelId);
        if (channel != null)
            await channel.SendMessageAsync($"A new presence enters: <@{_userId}>.\n\n" +
                "You have been marked as **The Uninitiated**.\n" +
                $"To walk among us, you must complete the **Rite of Choosing** in <#{_ritualChannelId}>.\n" +
                $"You have **{BotConfig.InitiationTimeoutHours} hours** before the veil closes.");
    }

    public async Task DeleteRitualAsync(ulong messageId)
    {
        var message = await RitualChannel.GetMessageAsync(messageId);
        if (message != null) await message.DeleteAsync();
    }

    public async Task AnnounceCompletionAsync(string key)
    {
        var role = GetRole(key);
        var text = $"<@{_userId}> has chosen the path of the **{role.Name}**.";
        if (Uri.TryCreate(role.Gif, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            text += $"\n{role.Gif}";
        await RitualChannel.SendMessageAsync(text + "\nGreet them.");
    }

    public async Task<bool> SendReminderAsync()
    {
        var text = $"<@{_userId}> - the veil grows thin. Your time to choose a path has all but slipped away.\n\n" +
            $"**Choose your role now** in <#{_ritualChannelId}>, or your time in this server will come to a close in **{BotConfig.ReminderGracePeriodHours} hours**. The Cult does not wait forever.";
        try { await RitualChannel.SendMessageAsync(text); return true; }
        catch (Exception ex) { Console.WriteLine($"Could not send initiation reminder: {ex.GetType().Name}"); }
        try
        {
            var dm = await Member.CreateDMChannelAsync();
            await dm.SendMessageAsync(text);
            return true;
        }
        catch (Exception ex) { Console.WriteLine($"Could not DM initiation reminder: {ex.GetType().Name}"); return false; }
    }

    public Task KickAsync() => Member.KickAsync("Failed to complete initiation within the allowed time");
    public async Task AnnounceExpirationAsync() =>
        await RitualChannel.SendMessageAsync($"<@{_userId}> has failed to complete the rites.\nThey have been cast out of the Cult.");

    private RestGuildUser Member => _member ?? throw new InvalidOperationException("Membership has not been verified.");
    private SocketTextChannel RitualChannel => _guild.GetTextChannel(_ritualChannelId) ??
        throw new InvalidOperationException("Ritual channel is unavailable.");
    private static (ulong Id, string Name, string Gif) GetRole(string key) => key switch
    {
        "SilentWitness" => (BotConfig.SilentWitnessRoleId, "Silent Witness", BotConfig.SilentWitnessGifUrl),
        "NeonDisciple" => (BotConfig.NeonDiscipleRoleId, "Neon Disciple", BotConfig.NeonDiscipleGifUrl),
        "VeiledArchivist" => (BotConfig.VeiledArchivistRoleId, "Veiled Archivist", BotConfig.VeiledArchivistGifUrl),
        _ => throw new ArgumentException("Unknown initiation path.", nameof(key))
    };
}
