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

        // Skip initiation for server owner and administrators
        if (user.Id == user.Guild.OwnerId || user.GuildPermissions.Administrator)
        {
            await _dataCollectionService.TrackUserJoinAsync(user);
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
            var clicker = interaction.User as SocketGuildUser;
            if (clicker == null) return;

            // Find who this ritual message belongs to (only that user can click)
            var session = await _initiationService.GetPendingSessionByRitualMessageAsync(clicker.Guild.Id, interaction.Message.Id);
            if (session == null)
            {
                await interaction.RespondAsync("This ritual message is no longer valid.", ephemeral: true);
                return;
            }

            if (clicker.Id != session.UserId)
            {
                await interaction.RespondAsync("Only the person this message is for can choose a role.", ephemeral: true);
                return;
            }

            var user = clicker;

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

            await interaction.DeferAsync(ephemeral: true);

            // Add the chosen role before removing the holding role or completing the session.
            var newRole = user.Guild.GetRole(chosenRoleId);
            if (newRole == null)
            {
                Console.WriteLine($"ERROR: Role '{chosenRoleName}' not found (ID: {chosenRoleId})");
                await interaction.FollowupAsync("That role is not configured correctly. Please contact an administrator.", ephemeral: true);
                return;
            }

            try
            {
                await user.AddRoleAsync(newRole);
                Console.WriteLine($"✓ Assigned '{chosenRoleName}' role to {user.Username}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not assign role '{chosenRoleName}' to {user.Username}: {ex.Message}");
                await interaction.FollowupAsync("I could not assign that role. Please contact an administrator.", ephemeral: true);
                return;
            }

            // Remove The Uninitiated role after the path role is safely assigned.
            var uninitiatedRole = user.Guild.GetRole(BotConfig.TheUninitiatedRoleId);
            if (uninitiatedRole != null && user.Roles.Contains(uninitiatedRole))
            {
                try
                {
                    await user.RemoveRoleAsync(uninitiatedRole);
                    Console.WriteLine($"✓ Removed 'The Uninitiated' role from {user.Username}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WARNING: Could not remove 'The Uninitiated' role from {user.Username}: {ex.Message}");
                }
            }

            // Delete the ritual message
            try
            {
                await interaction.Message.DeleteAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Could not delete ritual message {interaction.Message.Id}: {ex.Message}");
            }

            // Post success message with GIF
            var ritualChannel = user.Guild.GetTextChannel(BotConfig.RoleRitualChannelId);
            if (ritualChannel != null)
            {
                var successMessage = $"{user.Mention} has chosen the path of the **{chosenRoleName}**.";
                if (Uri.TryCreate(gifUrl, UriKind.Absolute, out _))
                    successMessage += $"\n{gifUrl}";
                successMessage += "\nGreet them.";
                await ritualChannel.SendMessageAsync(successMessage);
            }

            // Mark session as completed
            await _initiationService.MarkSessionCompletedAsync(session.Id, roleKey);
            Console.WriteLine($"✓ Initiation completed for {user.Username} - chose {chosenRoleName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleButtonInteractionAsync: {ex.Message}");
            try
            {
                await interaction.FollowupAsync("An error occurred. Please contact an administrator.", ephemeral: true);
            }
            catch
            {
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
}
