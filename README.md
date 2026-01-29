# ExpiredSodaCultBot 🥤

A Discord bot with a cult-themed onboarding system for the Expired Soda Cult server.

## Features

### 🔮 Cult Initiation System
- **24-hour role selection deadline** for new members
- **Three role paths**: Silent Witness, Neon Disciple, Veiled Archivist
- **Automatic expulsion** for members who don't complete the rites
- **Button-based interactions** for seamless role selection
- **PostgreSQL database** tracking all initiation sessions

### 👋 Welcome System
- Customized welcome messages in #gateway
- Ritual messages with interactive buttons in #role-ritual
- Role-specific success messages with GIFs

### 🔴 YouTube Live Stream Announcements
- **Automatic checking** every 10 minutes for live streams
- **Smart detection** - only announces once per stream
- **Manual `/live` command** for instant announcements
- **@everyone notifications** in #transmissions channel
- **Beautiful embeds** with stream links

## Setup

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
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

3. Update channel and role IDs in `CultBot/Configuration/BotConfig.cs` (see [SETUP.md](SETUP.md)).

4. Build and run the bot:
   ```bash
   cd CultBot
   dotnet build
   dotnet run
   ```

## Configuration

⚠️ **Important**: Before deploying, configure the bot with your Discord server IDs in `CultBot/Configuration/BotConfig.cs`.

**Full setup:** See **[SETUP.md](SETUP.md)** for deployment checklist, cult initiation, YouTube live, moderation, and database.

### Environment Variables

Required:
- `DISCORD_BOT_TOKEN` - Your Discord bot token
- `DATABASE_URL` - PostgreSQL connection string (automatically set by Railway)
- `YOUTUBE_API_KEY` - YouTube Data API v3 key (see [SETUP.md](SETUP.md))

## Bot Permissions

The bot requires the following Discord permissions:
- **Manage Roles** (to assign/remove roles)
- **Kick Members** (to remove members who fail initiation)
- View Channels
- Send Messages
- Read Message History
- Use Application Commands

Recommended permission integer: `2415958018`

Gateway Intents:
- Guilds
- Guild Members

## Project Structure

```
ExpiredSodaCultBot/
├── .github/
│   └── workflows/          # GitHub Actions
├── CultBot/
│   ├── Configuration/      # Bot configuration & constants
│   │   └── BotConfig.cs    # ⚠️ CONFIGURE THIS with your IDs
│   ├── Data/               # Database entities & context
│   │   ├── CultBotDbContext.cs
│   │   └── InitiationSession.cs
│   ├── Services/           # Business logic
│   │   ├── BotService.cs
│   │   ├── InitiationService.cs
│   │   ├── OnboardingService.cs
│   │   ├── InitiationExpirationService.cs
│   │   ├── YouTubeLiveService.cs
│   │   ├── LiveStreamAnnouncementService.cs
│   │   ├── LiveStreamCheckerService.cs
│   │   └── SlashCommandHandler.cs
│   ├── Program.cs          # Entry point & DI setup
│   └── CultBot.csproj      # Project configuration
├── deployment/             # Optional Docker (Railway doesn't need it)
├── SETUP.md                # Full setup guide
└── README.md
```

## Dependencies

- **Discord.Net** - Discord API client
- **Entity Framework Core** - Database ORM
- **Npgsql** - PostgreSQL provider
- **Microsoft.Extensions.Hosting** - Background services
- **Google.Apis.YouTube.v3** - YouTube Data API client

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

### Option 3: Docker (Optional)

Docker files are in `deployment/` if you prefer containerized deployment; Railway builds .NET natively and does not require Docker.

## Contributing

Feel free to open issues or submit pull requests!

## License

This project is open source and available for personal use.
