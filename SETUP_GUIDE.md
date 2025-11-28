# Cult Onboarding System - Setup Guide

## Overview

This implementation provides a complete cult-themed initiation system with:
- **24-hour deadline** for new members to choose a role
- **Three role paths**: Silent Witness, Neon Disciple, Veiled Archivist
- **Automatic expulsion** for members who don't complete initiation
- **PostgreSQL database** for tracking initiation sessions
- **Button interactions** for role selection

## Setup Steps

### 1. Configure Discord Server

#### Create Roles (if not already created):
1. **The Uninitiated** - Assigned to new members automatically
2. **Silent Witness** - For lurkers/watchers
3. **Neon Disciple** - For gamers
4. **Veiled Archivist** - For story/horror enthusiasts

#### Create Channels:
1. **#gateway** - Public entry channel for welcome messages
2. **#role-ritual** - Ritual chamber for role selection (should only be visible to The Uninitiated)

#### Set Channel Permissions:
For **#role-ritual**:
- Allow: The Uninitiated (Read, View Channel, Read Message History)
- Deny: @everyone

For other server channels:
- Deny: The Uninitiated
- Allow: Silent Witness, Neon Disciple, Veiled Archivist

### 2. Get Discord IDs

You need to obtain the following IDs (enable Developer Mode in Discord Settings > Advanced):

Right-click and "Copy ID" for each:
- **Gateway Channel ID** (channel)
- **Role Ritual Channel ID** (channel)
- **The Uninitiated Role ID** (role)
- **Silent Witness Role ID** (role)
- **Neon Disciple Role ID** (role)
- **Veiled Archivist Role ID** (role)

### 3. Configure BotConfig.cs

Open `CultBot/Configuration/BotConfig.cs` and replace the placeholder values:

```csharp
public const ulong GatewayChannelId =   ;
public const ulong RoleRitualChannelId = YOUR_ROLE_RITUAL_CHANNEL_ID;

public const ulong TheUninitiatedRoleId = YOUR_UNINITIATED_ROLE_ID;
public const ulong SilentWitnessRoleId = YOUR_SILENT_WITNESS_ROLE_ID;
public const ulong NeonDiscipleRoleId = YOUR_NEON_DISCIPLE_ROLE_ID;
public const ulong VeiledArchivistRoleId = YOUR_VEILED_ARCHIVIST_ROLE_ID;

public const string SilentWitnessGifUrl = "https://your-gif-url.com/silent.gif";
public const string NeonDiscipleGifUrl = "https://your-gif-url.com/neon.gif";
public const string VeiledArchivistGifUrl = "https://your-gif-url.com/veiled.gif";
```

### 4. Set Up PostgreSQL Database on Railway

In your Railway project dashboard:

1. Click **"New"** → **"Database"** → **"Add PostgreSQL"**
2. Railway will automatically create a `DATABASE_URL` environment variable
3. The bot will automatically connect to it

**Note**: The bot will use an in-memory database if `DATABASE_URL` is not set (for local testing only).

### 5. Bot Permissions

Ensure your bot has the following permissions in Discord:
- Manage Roles (to assign/remove roles)
- Kick Members (to remove members who fail initiation)
- Send Messages
- Read Message History
- View Channels
- Use Application Commands

Invite URL format:
```
https://discord.com/api/oauth2/authorize?client_id=YOUR_BOT_CLIENT_ID&permissions=2415958018&scope=bot
```

### 6. Deploy to Railway

The bot is already configured for Railway deployment. After pushing code:

1. Railway will automatically build and deploy
2. Add environment variable: `DISCORD_BOT_TOKEN` (if not already set)
3. The bot will automatically:
   - Connect to PostgreSQL
   - Create database tables
   - Start listening for new members

### 7. Testing Locally (Optional)

To test locally before deploying:

```bash
# Set environment variables
$env:DISCORD_BOT_TOKEN="your_bot_token"
$env:DATABASE_URL="postgresql://localhost:5432/cultbot"  # or omit for in-memory

# Restore packages
dotnet restore

# Run migrations (creates tables)
dotnet ef database update

# Run the bot
dotnet run --project CultBot/CultBot.csproj
```

## How It Works

### New Member Flow:

1. **User joins server** →
   - Assigned "The Uninitiated" role
   - Welcome message posted in #gateway
   - Ritual message with 3 buttons posted in #role-ritual
   - Session stored in database with join timestamp

2. **User clicks button (within 24 hours)** →
   - "The Uninitiated" role removed
   - Chosen role assigned
   - Ritual message deleted
   - Success message with GIF posted in #role-ritual
   - Session marked as "Completed" in database

3. **24 hours pass without selection** →
   - Background service detects expired session
   - Ritual message deleted
   - Failure message posted in #role-ritual
   - User kicked from server
   - Session marked as "Expired" in database

### Rejoin Behavior:

If a user leaves and rejoins:
- They are treated as a new member
- New initiation session created
- New ritual message sent
- Previous roles are NOT automatically restored

## Database Schema

### InitiationSessions Table

| Column | Type | Description |
|--------|------|-------------|
| Id | int | Primary key |
| UserId | ulong | Discord user ID |
| GuildId | ulong | Discord server ID |
| RitualChannelId | ulong | Channel where ritual message was sent |
| RitualMessageId | ulong | Message ID of the ritual message |
| JoinTimeUtc | DateTime | When user joined |
| Status | string | "Pending", "Completed", or "Expired" |
| ChosenRole | string | "SilentWitness", "NeonDisciple", "VeiledArchivist", or null |
| CompletedTimeUtc | DateTime? | When initiation was completed |
| ExpiredTimeUtc | DateTime? | When initiation expired |

## Customization

### Change Timeout Duration

In `BotConfig.cs`:
```csharp
public const int InitiationTimeoutHours = 24; // Change to desired hours
```

### Change Check Interval

In `BotConfig.cs`:
```csharp
public const int ExpirationCheckIntervalMinutes = 5; // How often to check for expired initiations
```

### Modify Messages

Edit the message templates in `OnboardingService.cs`:
- `HandleUserJoinedAsync` - Welcome and ritual messages
- `HandleButtonInteractionAsync` - Success messages
- `InitiationExpirationService.cs` - Failure messages

## Troubleshooting

### Bot doesn't assign roles
- Verify bot role is higher than the roles it's trying to assign in Discord server settings
- Check bot has "Manage Roles" permission

### Database errors
- Verify `DATABASE_URL` environment variable is set correctly
- Check Railway PostgreSQL database is running

### Buttons don't work
- Ensure bot has "Use Application Commands" permission
- Check console for interaction errors

### Members not kicked
- Verify bot has "Kick Members" permission
- Ensure bot role has sufficient permissions hierarchy

## Architecture

```
Program.cs
  ├── BotService (starts bot, wires events)
  ├── OnboardingService (handles joins & button clicks)
  ├── InitiationService (database operations)
  └── InitiationExpirationService (background worker for timeouts)

Data/
  ├── CultBotDbContext (EF Core context)
  └── InitiationSession (entity model)

Configuration/
  └── BotConfig (all constants & IDs)
```

## Next Steps After Setup

1. Test with a test Discord account:
   - Join the server
   - Verify welcome messages appear
   - Click a role button
   - Verify role is assigned

2. Test timeout:
   - Join with test account
   - Wait 24+ hours (or temporarily change `InitiationTimeoutHours` to 1 minute for testing)
   - Verify user is kicked

3. Monitor logs in Railway dashboard for any errors
