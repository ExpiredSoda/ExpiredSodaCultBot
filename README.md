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

## Deployment Options

### Option 1: Docker (Recommended)

The easiest way to run the bot 24/7 is using Docker:

1. Install [Docker](https://www.docker.com/get-started)
2. Create a `.env` file from the example:
   ```bash
   cp .env.example .env
   ```
3. Edit `.env` and add your Discord bot token
4. Run with Docker Compose:
   ```bash
   docker-compose up -d
   ```

To stop the bot:
```bash
docker-compose down
```

To view logs:
```bash
docker-compose logs -f
```

### Option 2: Railway (Free Cloud Hosting)

1. Create an account at [Railway.app](https://railway.app)
2. Click "New Project" → "Deploy from GitHub repo"
3. Select this repository
4. Add environment variable: `DISCORD_BOT_TOKEN` with your token
5. Railway will automatically deploy and keep your bot running 24/7

### Option 3: Render (Free Cloud Hosting)

1. Create an account at [Render.com](https://render.com)
2. Click "New +" → "Background Worker"
3. Connect your GitHub repository
4. Configure:
   - **Build Command**: `dotnet build CultBot/CultBot.csproj -c Release`
   - **Start Command**: `dotnet run --project CultBot/CultBot.csproj -c Release`
5. Add environment variable: `DISCORD_BOT_TOKEN`
6. Click "Create Background Worker"

### Option 4: Azure Container Instances

1. Build and push your Docker image:
   ```bash
   docker build -t expiredsodacultbot .
   docker tag expiredsodacultbot:latest <your-registry>/expiredsodacultbot:latest
   docker push <your-registry>/expiredsodacultbot:latest
   ```
2. Deploy to Azure Container Instances via Azure Portal or CLI
3. Set the `DISCORD_BOT_TOKEN` environment variable

### Option 5: VPS (DigitalOcean, Linode, etc.)

1. SSH into your VPS
2. Install Docker
3. Clone this repository
4. Follow Docker deployment steps above

## Contributing

Feel free to open issues or submit pull requests!

## License

This project is open source and available for personal use.
