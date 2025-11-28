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
                Console.WriteLine($"  {(permissions.ViewChannel ? "✓" : "✗")} View Channels");

                if (!permissions.ManageRoles || !permissions.KickMembers)
                {
                    Console.WriteLine("✗ WARNING: Bot is missing critical permissions!");
                    hasErrors = true;
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
}
