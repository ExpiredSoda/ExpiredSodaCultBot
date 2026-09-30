using CultBot.Configuration;
using Discord.WebSocket;

namespace CultBot.Services;

public class OnboardingService
{
    private readonly DiscordSocketClient _client;
    private readonly InitiationWorkflow _workflow;
    private readonly DataCollectionService _dataCollection;
    private readonly ConfigurationValidator _validator;

    public OnboardingService(DiscordSocketClient client, InitiationWorkflow workflow,
        DataCollectionService dataCollection, ConfigurationValidator validator)
    {
        _client = client;
        _workflow = workflow;
        _dataCollection = dataCollection;
        _validator = validator;
    }

    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        if (user.IsBot) return;
        await _dataCollection.TrackUserJoinAsync(user);
        try
        {
            var outcome = await _workflow.RecoverAsync(user.Guild.Id, user.Id, BotConfig.RoleRitualChannelId,
                CreateActions(user.Guild, user.Id));
            Console.WriteLine($"New-member initiation: {outcome}");
        }
        catch (Exception ex) { Console.WriteLine($"Error starting initiation: {ex.GetType().Name}"); }
    }

    public async Task HandleButtonInteractionAsync(SocketMessageComponent interaction)
    {
        var roleKey = interaction.Data.CustomId switch
        {
            BotConfig.ButtonSilentWitness => "SilentWitness",
            BotConfig.ButtonNeonDisciple => "NeonDisciple",
            BotConfig.ButtonVeiledArchivist => "VeiledArchivist",
            _ => null
        };
        if (roleKey == null || interaction.User is not SocketGuildUser user) return;
        try
        {
            await interaction.DeferAsync(ephemeral: true);
            var outcome = await _workflow.CompleteAsync(user.Guild.Id, user.Id, interaction.Message.Id,
                roleKey, CreateActions(user.Guild, user.Id));
            var response = outcome switch
            {
                InitiationOutcome.Completed => "Your initiation is complete. Welcome to the Cult.",
                InitiationOutcome.ConfigurationDisabled => "Initiation is temporarily unavailable. Please contact an administrator.",
                _ => "This ritual is no longer active or belongs to another member."
            };
            await interaction.FollowupAsync(response, ephemeral: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error completing initiation: {ex.GetType().Name}");
            try
            {
                const string message = "I could not finish saving your initiation. Please try your ritual again or contact an administrator.";
                if (interaction.HasResponded) await interaction.FollowupAsync(message, ephemeral: true);
                else await interaction.RespondAsync(message, ephemeral: true);
            }
            catch { /* The interaction may no longer be available. */ }
        }
    }

    public DiscordInitiationActions CreateActions(SocketGuild guild, ulong userId, ulong? ritualChannelId = null) =>
        new(_client, guild, userId, ritualChannelId ?? BotConfig.RoleRitualChannelId, _validator);
}
