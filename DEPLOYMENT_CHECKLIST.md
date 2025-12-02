# 🚀 Deployment Checklist

## Before Deploying to Railway

### 1. Discord Developer Portal Configuration
https://discord.com/developers/applications

#### Enable Privileged Intents (CRITICAL)
Navigate to: Bot → Privileged Gateway Intents
- ☑️ **PRESENCE INTENT** - Required for game tracking
- ☑️ **SERVER MEMBERS INTENT** - Required for join/leave tracking  
- ☑️ **MESSAGE CONTENT INTENT** - Required for spam/profanity detection

⚠️ **Without these intents, moderation features will not work!**

#### Bot Permissions
Invite the bot with these permissions:
- Manage Roles
- Kick Members
- Ban Members
- Manage Messages
- Send Messages
- View Channels
- Read Message History

Use this permission calculator: https://discordapi.com/permissions.html
**Required Integer:** `268445718`

### 2. Railway Environment Variables
Set these in your Railway project:

```
DISCORD_BOT_TOKEN=your_bot_token_here
DATABASE_URL=postgresql://... (auto-configured by Railway)
YOUTUBE_API_KEY=your_youtube_api_key_here
```

### 3. Discord Server Setup

#### Create Channels
1. **#gateway** - Public onboarding channel (users can't send messages)
2. **#role-ritual** - Private initiation channel (only TheUninitiated can see)
3. **#transmissions** - Public announcements channel
4. **#mod-logs** - Private moderation log channel (admin only)

#### Create Roles
1. **The Uninitiated** - Auto-assigned on join, removed after choosing a path
2. **Silent Witness** - First path option
3. **Neon Disciple** - Second path option
4. **Veiled Archivist** - Third path option

#### Get Channel & Role IDs
1. Enable Developer Mode: Discord Settings → Advanced → Developer Mode ✅
2. Right-click each channel → Copy Channel ID
3. Right-click each role → Copy Role ID

### 4. Update BotConfig.cs

Replace placeholder IDs with your actual Discord IDs:

```csharp
// Channel IDs
public static readonly ulong GatewayChannelId = YOUR_GATEWAY_CHANNEL_ID;
public static readonly ulong RoleRitualChannelId = YOUR_ROLE_RITUAL_CHANNEL_ID;
public static readonly ulong TransmissionsChannelId = YOUR_TRANSMISSIONS_CHANNEL_ID;
public static readonly ulong ModLogChannelId = YOUR_MOD_LOG_CHANNEL_ID;

// Role IDs  
public static readonly ulong TheUninitiatedRoleId = YOUR_UNINITIATED_ROLE_ID;
public static readonly ulong SilentWitnessRoleId = YOUR_SILENT_WITNESS_ROLE_ID;
public static readonly ulong NeonDiscipleRoleId = YOUR_NEON_DISCIPLE_ROLE_ID;
public static readonly ulong VeiledArchivistRoleId = YOUR_VEILED_ARCHIVIST_ROLE_ID;
```

### 5. Configure Moderation Settings (Optional)

#### Profanity Filter
Edit `BotConfig.cs` to add filtered terms:
```csharp
public static readonly string[] RacialSlurs = new[]
{
    "term1",
    "term2",
    // Add more terms here
};
```

#### Spam Thresholds (defaults are fine for most servers)
```csharp
public static readonly int SpamMessageThreshold = 5;        // Messages before spam
public static readonly int SpamTimeWindowSeconds = 10;      // Time window
public static readonly int BotBanThreshold = 25;            // Score to auto-ban
```

#### Game Tracking
Add games you want to track:
```csharp
public static readonly string[] TrackedGames = new[]
{
    "Halo",
    "Destiny 2",
    "Call of Duty",
    // Add more games
};
```

### 6. Commit Configuration Changes

```bash
git add CultBot/Configuration/BotConfig.cs
git commit -m "Configure Discord IDs for production"
git push origin master
```

Railway will automatically redeploy with your changes.

### 7. Verify Deployment

#### Check Railway Logs
Look for these startup messages:
```
Database initialized.
Connected as YourBotName#1234
Cult Bot is ready!
✓ Channel 'gateway' found
✓ Channel 'role-ritual' found
✓ Role 'The Uninitiated' found
...
```

#### Test Features
1. **Join Detection**: Invite a test account → should receive TheUninitiated role
2. **Initiation**: Click role buttons → should assign role and remove TheUninitiated
3. **YouTube Check**: Run `/live` command → should check for streams
4. **Spam Detection**: Send 5+ messages quickly → should trigger warning
5. **Game Tracking**: Play a game → check logs for tracking message

### 8. Monitor & Debug

#### Railway Dashboard
- View real-time logs
- Check resource usage
- Monitor deployments

#### Common Issues
- **Bot offline**: Check DISCORD_BOT_TOKEN is correct
- **Intents error**: Enable privileged intents in Developer Portal
- **Database errors**: Railway DATABASE_URL should auto-configure
- **Role/Channel not found**: Verify IDs in BotConfig.cs match Discord

## Production Checklist

- [ ] Privileged intents enabled in Discord Developer Portal
- [ ] Bot invited with correct permissions (268445718)
- [ ] All channels created (#gateway, #role-ritual, #transmissions, #mod-logs)
- [ ] All roles created (The Uninitiated, Silent Witness, Neon Disciple, Veiled Archivist)
- [ ] Channel IDs copied and updated in BotConfig.cs
- [ ] Role IDs copied and updated in BotConfig.cs
- [ ] Environment variables set in Railway (DISCORD_BOT_TOKEN, YOUTUBE_API_KEY)
- [ ] Changes committed and pushed to GitHub
- [ ] Railway deployment succeeded
- [ ] Bot shows as online in Discord
- [ ] Configuration validator shows all ✓ in logs
- [ ] Test user join triggers initiation flow
- [ ] Moderation features tested (spam, game tracking)

## Support Resources

- **Discord Developer Docs**: https://discord.com/developers/docs
- **Railway Docs**: https://docs.railway.app
- **Bot Setup Guide**: See MODERATION_SETUP.md
- **GitHub Repo**: https://github.com/ExpiredSoda/ExpiredSodaCultBot
