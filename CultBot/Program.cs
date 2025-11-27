using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

class Program
{
    private DiscordSocketClient _client;

    // TODO: replace this with your actual channel ID (as a ulong)
    private const ulong WelcomeChannelId = 1442967396238229514;

    public static Task Main(string[] args) => new Program().MainAsync();

    public async Task MainAsync()
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds |
                             GatewayIntents.GuildMembers
        };

        _client = new DiscordSocketClient(config);

        _client.Log += LogAsync;
        _client.Ready += OnReadyAsync;
        _client.UserJoined += OnUserJoinedAsync;

        // Read bot token from environment variable
        var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Bot token not set. Set the DISCORD_BOT_TOKEN environment variable.");
            return;
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        Console.WriteLine("Cult Bot is running. Press Enter to exit.");
        Console.ReadLine();
    }

    private Task LogAsync(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private Task OnReadyAsync()
    {
        Console.WriteLine($"Connected as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
        return Task.CompletedTask;
    }

    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
    // Find the welcome channel by ID
    var channel = _client.GetChannel(WelcomeChannelId) as IMessageChannel;

    if (channel == null)
    {
        Console.WriteLine("Welcome channel not found. Check the ID.");
        return;
    }

    // Short, social-focused welcome message
    var message = $"{user.Mention} has joined the Cult. 🥤\n" +
                  "Everyone say hi and drop a 👋 or your favorite cursed gif.";

    // Send the message
    var sentMessage = await channel.SendMessageAsync(message);

    // Add a wave reaction so people can just tap it
    await sentMessage.AddReactionAsync(new Emoji("👋"));
    }
}
