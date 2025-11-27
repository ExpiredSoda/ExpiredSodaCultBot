# ExpiredSodaCultBot 🥤

A Discord bot that welcomes new members to the Expired Soda Cult server with a fun greeting message and wave reaction.

## Features

- Welcomes new members when they join the server
- Posts a customized welcome message in a designated channel
- Automatically adds a 👋 reaction for easy community engagement

## Setup

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- A Discord Bot Token (from [Discord Developer Portal](https://discord.com/developers/applications))

### Installation

1. Clone this repository:
   ```bash
   git clone https://github.com/YOUR_USERNAME/ExpiredSodaCultBot.git
   cd ExpiredSodaCultBot
   ```

2. Set your Discord bot token as an environment variable:
   
   **Windows (PowerShell):**
   ```powershell
   $env:DISCORD_BOT_TOKEN="your_bot_token_here"
   ```
   
   **Linux/Mac:**
   ```bash
   export DISCORD_BOT_TOKEN="your_bot_token_here"
   ```

3. Update the welcome channel ID in `CultBot/Program.cs`:
   ```csharp
   private const ulong WelcomeChannelId = YOUR_CHANNEL_ID;
   ```

4. Build and run the bot:
   ```bash
   cd CultBot
   dotnet build
   dotnet run
   ```

## Configuration

- **Welcome Channel ID**: Set in `Program.cs` - replace `WelcomeChannelId` with your server's welcome channel ID
- **Bot Token**: Set via `DISCORD_BOT_TOKEN` environment variable

## Bot Permissions

The bot requires the following Discord permissions:
- View Channels
- Send Messages
- Add Reactions
- Read Message History

And these Gateway Intents:
- Guilds
- Guild Members

## Project Structure

```
ExpiredSodaCultBot/
├── CultBot/
│   ├── Program.cs          # Main bot logic
│   └── CultBot.csproj      # Project configuration
└── ExpiredSodaCultBot.sln  # Solution file
```

## Dependencies

- Discord.Net (WebSocket client for Discord)

## Contributing

Feel free to open issues or submit pull requests!

## License

This project is open source and available for personal use.
