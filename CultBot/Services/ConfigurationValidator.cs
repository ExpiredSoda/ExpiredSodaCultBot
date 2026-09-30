using CultBot.Configuration;
using Discord.WebSocket;

namespace CultBot.Services;

public class ConfigurationValidator
{
    private readonly DiscordSocketClient _client;

    public ConfigurationValidator(DiscordSocketClient client)
    {
        _client = client;
    }

    public async Task ValidateConfigurationAsync()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Running Configuration Validation...");
        Console.WriteLine("========================================");

        var hasErrors = false;

        // Validate each guild the bot is in
        foreach (var guild in _client.Guilds)
        {
            Console.WriteLine($"\nValidating configuration for guild: {guild.Name} (ID: {guild.Id})");
            var initiationSafe = IsInitiationConfigurationSafe(guild, out var initiationReason);
            Console.WriteLine(initiationSafe ? "Initiation actions enabled: safety checks passed." :
                $"Initiation actions disabled: {initiationReason}");
            hasErrors |= !initiationSafe;

            // Check Gateway Channel
            var gatewayChannel = guild.GetTextChannel(BotConfig.GatewayChannelId);
            if (gatewayChannel != null)
            {
                Console.WriteLine($"✓ Gateway Channel: #{gatewayChannel.Name} (ID: {BotConfig.GatewayChannelId})");
            }
            else
            {
                Console.WriteLine($"✗ ERROR: Gateway Channel not found! (ID: {BotConfig.GatewayChannelId})");
                hasErrors = true;
            }

            // Check Role Ritual Channel
            var ritualChannel = guild.GetTextChannel(BotConfig.RoleRitualChannelId);
            if (ritualChannel != null)
            {
                Console.WriteLine($"✓ Role Ritual Channel: #{ritualChannel.Name} (ID: {BotConfig.RoleRitualChannelId})");
            }
            else
            {
                Console.WriteLine($"✗ ERROR: Role Ritual Channel not found! (ID: {BotConfig.RoleRitualChannelId})");
                hasErrors = true;
            }

            // Check Transmissions Channel
            var transmissionsChannel = guild.GetTextChannel(BotConfig.TransmissionsChannelId);
            if (transmissionsChannel != null)
            {
                Console.WriteLine($"✓ Transmissions Channel: #{transmissionsChannel.Name} (ID: {BotConfig.TransmissionsChannelId})");
            }
            else
            {
                Console.WriteLine($"✗ ERROR: Transmissions Channel not found! (ID: {BotConfig.TransmissionsChannelId})");
                hasErrors = true;
            }

            if (BotConfig.MemesChannelId == 0)
            {
                Console.WriteLine("Memes Channel: disabled (MemesChannelId is 0)");
            }
            else
            {
                var memesChannel = guild.GetTextChannel(BotConfig.MemesChannelId);
                if (memesChannel != null)
                {
                    Console.WriteLine($"✓ Memes Channel: #{memesChannel.Name} (ID: {BotConfig.MemesChannelId})");
                }
                else
                {
                    Console.WriteLine($"✗ ERROR: Memes Channel not found! (ID: {BotConfig.MemesChannelId})");
                    hasErrors = true;
                }

                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BotConfig.TumblrConsumerKeyEnvironmentVariable)))
                {
                    Console.WriteLine("Memes Source: disabled (TUMBLR_CONSUMER_KEY is missing)");
                }
            }

            // Check The Uninitiated Role
            var uninitiatedRole = guild.GetRole(BotConfig.TheUninitiatedRoleId);
            if (uninitiatedRole != null)
            {
                Console.WriteLine($"✓ The Uninitiated Role: @{uninitiatedRole.Name} (ID: {BotConfig.TheUninitiatedRoleId})");
            }
            else
            {
                Console.WriteLine($"✗ ERROR: The Uninitiated Role not found! (ID: {BotConfig.TheUninitiatedRoleId})");
                hasErrors = true;
            }

            // Check Silent Witness Role
            var silentWitnessRole = guild.GetRole(BotConfig.SilentWitnessRoleId);
            if (silentWitnessRole != null)
            {
                Console.WriteLine($"✓ Silent Witness Role: @{silentWitnessRole.Name} (ID: {BotConfig.SilentWitnessRoleId})");
            }
            else
            {
                Console.WriteLine($"✗ ERROR: Silent Witness Role not found! (ID: {BotConfig.SilentWitnessRoleId})");
                hasErrors = true;
            }

            // Check Neon Disciple Role
            var neonDiscipleRole = guild.GetRole(BotConfig.NeonDiscipleRoleId);
            if (neonDiscipleRole != null)
            {
                Console.WriteLine($"✓ Neon Disciple Role: @{neonDiscipleRole.Name} (ID: {BotConfig.NeonDiscipleRoleId})");
            }
            else
            {
                Console.WriteLine($"✗ ERROR: Neon Disciple Role not found! (ID: {BotConfig.NeonDiscipleRoleId})");
                hasErrors = true;
            }

            // Check Veiled Archivist Role
            var veiledArchivistRole = guild.GetRole(BotConfig.VeiledArchivistRoleId);
            if (veiledArchivistRole != null)
            {
                Console.WriteLine($"✓ Veiled Archivist Role: @{veiledArchivistRole.Name} (ID: {BotConfig.VeiledArchivistRoleId})");
            }
            else
            {
                Console.WriteLine($"✗ ERROR: Veiled Archivist Role not found! (ID: {BotConfig.VeiledArchivistRoleId})");
                hasErrors = true;
            }

            // Check bot permissions
            var botUser = guild.GetUser(_client.CurrentUser.Id);
            if (botUser != null)
            {
                var permissions = botUser.GuildPermissions;
                
                Console.WriteLine("\nBot Permissions Check:");
                Console.WriteLine($"  {(permissions.ManageRoles ? "✓" : "✗")} Manage Roles");
                Console.WriteLine($"  {(permissions.KickMembers ? "✓" : "✗")} Kick Members");
                Console.WriteLine($"  {(permissions.SendMessages ? "✓" : "✗")} Send Messages");
                Console.WriteLine($"  {(permissions.AttachFiles ? "✓" : "✗")} Attach Files");
                Console.WriteLine($"  {(permissions.ViewChannel ? "✓" : "✗")} View Channels");

                if (!permissions.ManageRoles || !permissions.KickMembers)
                {
                    Console.WriteLine("✗ WARNING: Bot is missing critical permissions!");
                    hasErrors = true;
                }

                if (BotConfig.MemesChannelId != 0)
                {
                    var memesChannel = guild.GetTextChannel(BotConfig.MemesChannelId);
                    if (memesChannel != null)
                    {
                        var channelPermissions = botUser.GetPermissions(memesChannel);
                        if (!channelPermissions.ViewChannel || !channelPermissions.SendMessages || !channelPermissions.AttachFiles)
                        {
                            Console.WriteLine("✗ WARNING: Bot is missing View Channel, Send Messages, or Attach Files in the memes channel.");
                            hasErrors = true;
                        }
                    }
                }
            }
        }

        Console.WriteLine("\n========================================");
        if (hasErrors)
        {
            Console.WriteLine("⚠️  CONFIGURATION ERRORS DETECTED!");
            Console.WriteLine("Please update BotConfig.cs with correct IDs");
            Console.WriteLine("and ensure the bot has proper permissions.");
        }
        else
        {
            Console.WriteLine("✓ All configuration checks passed!");
        }
        Console.WriteLine("========================================\n");

        await Task.CompletedTask;
    }

    public bool IsInitiationConfigurationSafe(SocketGuild guild, out string reason)
    {
        var ids = new[] { BotConfig.TheUninitiatedRoleId, BotConfig.SilentWitnessRoleId,
            BotConfig.NeonDiscipleRoleId, BotConfig.VeiledArchivistRoleId };
        var roles = ids.Select(guild.GetRole).ToArray();
        var bot = _client.CurrentUser == null ? null : guild.GetUser(_client.CurrentUser.Id);
        var botPosition = bot?.Roles.Max(role => role.Position) ?? -1;
        var channel = guild.GetTextChannel(BotConfig.RoleRitualChannelId);
        var channelPermissions = bot != null && channel != null ? bot.GetPermissions(channel) : default;
        var rolesPresent = roles.All(role => role != null);
        var rolesManageable = roles.All(role => role != null && !role.IsManaged &&
            !role.Permissions.Administrator && role.Position < botPosition);
        var manageRoles = bot?.GuildPermissions.ManageRoles == true;
        var kickMembers = bot?.GuildPermissions.KickMembers == true;
        var ritualAccessible = channel != null && channelPermissions.ViewChannel && channelPermissions.SendMessages &&
            channelPermissions.ReadMessageHistory;
        var distinct = ids.All(id => id != 0) && ids.Distinct().Count() == ids.Length;
        var safe = InitiationPolicy.IsConfigurationSafe(rolesPresent, rolesManageable, manageRoles, kickMembers,
            ritualAccessible, distinct, BotConfig.InitiationTimeoutHours, BotConfig.ReminderGracePeriodHours,
            BotConfig.RecoveryMaxJoinAgeDays);
        var failures = new List<string>();
        if (!rolesPresent || !distinct) failures.Add("initiation role IDs are missing or duplicated");
        if (!rolesManageable) failures.Add("initiation roles must be ordinary roles below the bot and must not grant Administrator");
        if (!manageRoles || !kickMembers) failures.Add("bot requires Manage Roles and Kick Members");
        if (!ritualAccessible) failures.Add("ritual channel requires View Channel, Send Messages and Read Message History");
        var windowsPositive = BotConfig.InitiationTimeoutHours > 0 && BotConfig.ReminderGracePeriodHours > 0 && BotConfig.RecoveryMaxJoinAgeDays > 0;
        if (!windowsPositive)
            failures.Add("initiation timeouts and recovery age must be positive");
        reason = string.Join("; ", failures);
        return safe;
    }
}
