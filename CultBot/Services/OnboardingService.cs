using CultBot.Configuration;
using CultBot.Data;
using Discord;
using Discord.WebSocket;

namespace CultBot.Services;

public class OnboardingService
{
    private readonly DiscordSocketClient _client;
    private readonly InitiationService _initiationService;
    private readonly DataCollectionService _dataCollectionService;

    public OnboardingService(DiscordSocketClient client, InitiationService initiationService, DataCollectionService dataCollectionService)
    {
        _client = client;
        _initiationService = initiationService;
        _dataCollectionService = dataCollectionService;
    }

    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        // Ignore bots
        if (user.IsBot)
        {
            Console.WriteLine($"Ignoring bot user: {user.Username} (ID: {user.Id})");
            return;
        }

        // Track user join
        await _dataCollectionService.TrackUserJoinAsync(user);

        try
        {
            Console.WriteLine($"Processing new member: {user.Username} (ID: {user.Id}) in guild {user.Guild.Name}");

            // 1. Assign "The Uninitiated" role
            var uninitiatedRole = user.Guild.GetRole(BotConfig.TheUninitiatedRoleId);
            if (uninitiatedRole != null)
            {
                await user.AddRoleAsync(uninitiatedRole);
                Console.WriteLine($"✓ Assigned 'The Uninitiated' role to {user.Username}");
            }
            else
            {
                Console.WriteLine($"ERROR: The Uninitiated role not found (ID: {BotConfig.TheUninitiatedRoleId})");
            }

            // 2. Send welcome message in #gateway
            var gatewayChannel = user.Guild.GetTextChannel(BotConfig.GatewayChannelId);
            if (gatewayChannel != null)
            {
                var welcomeMessage = $"A new presence enters: {user.Mention}.\n\n" +
                                   $"You have been marked as **The Uninitiated**.\n" +
                                   $"To walk among us, you must complete the **Rite of Choosing** in <#{BotConfig.RoleRitualChannelId}>.\n" +
                                   $"You have **{BotConfig.InitiationTimeoutHours} hours** before the veil closes.";

                await gatewayChannel.SendMessageAsync(welcomeMessage);
                Console.WriteLine($"✓ Sent welcome message to #gateway");
            }
            else
            {
                Console.WriteLine($"ERROR: Gateway channel not found (ID: {BotConfig.GatewayChannelId})");
            }

            // 3. Send ritual message in #role-ritual with buttons
            var ritualChannel = user.Guild.GetTextChannel(BotConfig.RoleRitualChannelId);
            if (ritualChannel != null)
            {
                var ritualMessage = $"{user.Mention}, choose your path to enter the Cult.\n\n" +
                                  $"**Silent Witness** — for those who watch from the shadows.\n" +
                                  $"**Neon Disciple** — for those who challenge themselves in digital arenas.\n" +
                                  $"**Veiled Archivist** — for those who seek stories, lore, and horror.\n\n" +
                                  $"Select one below.\n" +
                                  $"You have **{BotConfig.InitiationTimeoutHours} hours**.";

                var component = new ComponentBuilder()
                    .WithButton("Become a Silent Witness", BotConfig.ButtonSilentWitness, ButtonStyle.Secondary)
                    .WithButton("Become a Neon Disciple", BotConfig.ButtonNeonDisciple, ButtonStyle.Secondary)
                    .WithButton("Become a Veiled Archivist", BotConfig.ButtonVeiledArchivist, ButtonStyle.Secondary)
                    .Build();

                var sentMessage = await ritualChannel.SendMessageAsync(ritualMessage, components: component);
                Console.WriteLine($"✓ Sent ritual message (ID: {sentMessage.Id}) in #role-ritual");

                // 4. Store the initiation session in the database
                var session = await _initiationService.CreateSessionAsync(
                    user.Id,
                    user.Guild.Id,
                    ritualChannel.Id,
                    sentMessage.Id
                );
                Console.WriteLine($"✓ Created initiation session (ID: {session.Id}) for {user.Username}. Expires in {BotConfig.InitiationTimeoutHours} hours.");
            }
            else
            {
                Console.WriteLine($"ERROR: Role ritual channel not found (ID: {BotConfig.RoleRitualChannelId})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleUserJoinedAsync: {ex.Message}");
        }
    }

    /// <summary>Send the usual welcome message in #gateway (same as new join).</summary>
    public async Task SendGatewayWelcomeForUserAsync(SocketGuildUser user)
    {
        var gatewayChannel = user.Guild.GetTextChannel(BotConfig.GatewayChannelId);
        if (gatewayChannel == null)
        {
            Console.WriteLine($"ERROR: Gateway channel not found for recovery (ID: {BotConfig.GatewayChannelId})");
            return;
        }
        var welcomeMessage = $"A new presence enters: {user.Mention}.\n\n" +
            $"You have been marked as **The Uninitiated**.\n" +
            $"To walk among us, you must complete the **Rite of Choosing** in <#{BotConfig.RoleRitualChannelId}>.\n" +
            $"You have **{BotConfig.InitiationTimeoutHours} hours** before the veil closes.";
        await gatewayChannel.SendMessageAsync(welcomeMessage);
        Console.WriteLine($"✓ Recovery: sent welcome message to #gateway for {user.Username}");
    }

    /// <summary>Send the ritual message and create a pending session for a user who has The Uninitiated role but no session (e.g. missed during bot downtime).</summary>
    public async Task<bool> SendRitualForUninitiatedUserAsync(SocketGuildUser user)
    {
        var ritualChannel = user.Guild.GetTextChannel(BotConfig.RoleRitualChannelId);
        if (ritualChannel == null)
        {
            Console.WriteLine($"ERROR: Role ritual channel not found for recovery (ID: {BotConfig.RoleRitualChannelId})");
            return false;
        }

        var ritualMessage = $"{user.Mention}, choose your path to enter the Cult.\n\n" +
            $"**Silent Witness** — for those who watch from the shadows.\n" +
            $"**Neon Disciple** — for those who challenge themselves in digital arenas.\n" +
            $"**Veiled Archivist** — for those who seek stories, lore, and horror.\n\n" +
            $"Select one below.\n" +
            $"You have **{BotConfig.InitiationTimeoutHours} hours**.";

        var component = new ComponentBuilder()
            .WithButton("Become a Silent Witness", BotConfig.ButtonSilentWitness, ButtonStyle.Secondary)
            .WithButton("Become a Neon Disciple", BotConfig.ButtonNeonDisciple, ButtonStyle.Secondary)
            .WithButton("Become a Veiled Archivist", BotConfig.ButtonVeiledArchivist, ButtonStyle.Secondary)
            .Build();

        var sentMessage = await ritualChannel.SendMessageAsync(ritualMessage, components: component);
        var session = await _initiationService.CreateSessionAsync(user.Id, user.Guild.Id, ritualChannel.Id, sentMessage.Id);
        Console.WriteLine($"✓ Recovery: sent ritual for {user.Username} (session ID: {session.Id}). Expires in {BotConfig.InitiationTimeoutHours} hours.");
        return true;
    }

    public async Task HandleButtonInteractionAsync(SocketMessageComponent interaction)
    {
        try
        {
            var user = interaction.User as SocketGuildUser;
            if (user == null) return;

            // Get the pending session for this user
            var session = await _initiationService.GetPendingSessionAsync(user.Id, user.Guild.Id);
            if (session == null)
            {
                await interaction.RespondAsync("You don't have a pending initiation.", ephemeral: true);
                return;
            }

            // Verify this is their ritual message
            if (interaction.Message.Id != session.RitualMessageId)
            {
                await interaction.RespondAsync("This is not your ritual message.", ephemeral: true);
                return;
            }

            // Determine which role was chosen
            string chosenRoleName;
            ulong chosenRoleId;
            string gifUrl;
            string roleKey;

            switch (interaction.Data.CustomId)
            {
                case BotConfig.ButtonSilentWitness:
                    chosenRoleName = "Silent Witness";
                    chosenRoleId = BotConfig.SilentWitnessRoleId;
                    gifUrl = BotConfig.SilentWitnessGifUrl;
                    roleKey = "SilentWitness";
                    break;
                case BotConfig.ButtonNeonDisciple:
                    chosenRoleName = "Neon Disciple";
                    chosenRoleId = BotConfig.NeonDiscipleRoleId;
                    gifUrl = BotConfig.NeonDiscipleGifUrl;
                    roleKey = "NeonDisciple";
                    break;
                case BotConfig.ButtonVeiledArchivist:
                    chosenRoleName = "Veiled Archivist";
                    chosenRoleId = BotConfig.VeiledArchivistRoleId;
                    gifUrl = BotConfig.VeiledArchivistGifUrl;
                    roleKey = "VeiledArchivist";
                    break;
                default:
                    return;
            }

            // Remove The Uninitiated role
            var uninitiatedRole = user.Guild.GetRole(BotConfig.TheUninitiatedRoleId);
            if (uninitiatedRole != null && user.Roles.Contains(uninitiatedRole))
            {
                await user.RemoveRoleAsync(uninitiatedRole);
                Console.WriteLine($"✓ Removed 'The Uninitiated' role from {user.Username}");
            }

            // Add the chosen role
            var newRole = user.Guild.GetRole(chosenRoleId);
            if (newRole != null)
            {
                await user.AddRoleAsync(newRole);
                Console.WriteLine($"✓ Assigned '{chosenRoleName}' role to {user.Username}");
            }
            else
            {
                Console.WriteLine($"ERROR: Role '{chosenRoleName}' not found (ID: {chosenRoleId})");
            }

            // Delete the ritual message
            await interaction.Message.DeleteAsync();

            // Post success message with GIF
            var ritualChannel = user.Guild.GetTextChannel(BotConfig.RoleRitualChannelId);
            if (ritualChannel != null)
            {
                var successMessage = $"{user.Mention} has chosen the path of the **{chosenRoleName}**.\n{gifUrl}\nGreet them.";
                await ritualChannel.SendMessageAsync(successMessage);
            }

            // Mark session as completed
            await _initiationService.MarkSessionCompletedAsync(session.Id, roleKey);
            Console.WriteLine($"✓ Initiation completed for {user.Username} - chose {chosenRoleName}");

            // Acknowledge the interaction
            await interaction.DeferAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleButtonInteractionAsync: {ex.Message}");
            try
            {
                await interaction.RespondAsync("An error occurred. Please contact an administrator.", ephemeral: true);
            }
            catch
            {
                // Interaction may have already been acknowledged
            }
        }
    }
}
