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
├── .github/
│   └── workflows/          # GitHub Actions (Docker build)
├── CultBot/
│   ├── Program.cs          # Main bot logic
│   └── CultBot.csproj      # Project configuration
├── deployment/             # Optional Docker files
│   ├── Dockerfile
│   ├── docker-compose.yml
│   └── README.md
├── .env.example            # Environment variable template
├── .gitignore
├── ExpiredSodaCultBot.sln  # Solution file
└── README.md
```

## Dependencies

- Discord.Net (WebSocket client for Discord)

## Deployment (24/7 Hosting)

### ⭐ Railway (Recommended - Easiest!)

Railway automatically detects .NET projects and deploys them - no Docker needed!

1. Create an account at [Railway.app](https://railway.app)
2. Click "New Project" → "Deploy from GitHub repo"
3. Select this repository
4. Add environment variable: `DISCORD_BOT_TOKEN` = your token
5. Done! Railway automatically builds and runs your bot 24/7

Railway will auto-redeploy whenever you push changes to GitHub.

### Option 2: Render (Free Cloud Hosting)

1. Create an account at [Render.com](https://render.com)
2. Click "New +" → "Background Worker"
3. Connect your GitHub repository
4. Configure:
   - **Build Command**: `dotnet build CultBot/CultBot.csproj -c Release`
   - **Start Command**: `dotnet run --project CultBot/CultBot.csproj -c Release`
5. Add environment variable: `DISCORD_BOT_TOKEN`
6. Click "Create Background Worker"

### Option 3: Docker (For Advanced Users)

If you prefer Docker, see the `deployment/` folder for Docker files and instructions.

## Contributing

Feel free to open issues or submit pull requests!

## License

This project is open source and available for personal use.
