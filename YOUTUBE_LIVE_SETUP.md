# YouTube Live Stream Announcements - Setup Guide

## Overview

The bot now automatically checks your YouTube channel every 10 minutes to see if you're live, and sends announcements to the #transmissions channel when a stream starts. You can also trigger announcements manually with the `/live` command.

## Features

✅ **Automatic checking** every 10 minutes  
✅ **Smart detection** - only announces once per stream  
✅ **Manual trigger** - `/live` slash command (admin only)  
✅ **Database tracking** - prevents duplicate announcements  
✅ **Beautiful embeds** - red-themed with YouTube branding  
✅ **@everyone ping** - notifies all members

---

## Setup Steps

### 1. Get a YouTube API Key

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project (or select existing)
3. Enable **YouTube Data API v3**:
   - Go to "APIs & Services" → "Library"
   - Search for "YouTube Data API v3"
   - Click "Enable"
4. Create credentials:
   - Go to "APIs & Services" → "Credentials"
   - Click "Create Credentials" → "API Key"
   - Copy the API key
   - (Optional) Restrict the key to only "YouTube Data API v3"

### 2. Configure Channel IDs

Open `CultBot/Configuration/BotConfig.cs` and set:

```csharp
// Find your #transmissions channel ID
public const ulong TransmissionsChannelId = YOUR_CHANNEL_ID_HERE;

// YouTube handle is already set to @ExpiredSodaOfficial
public const string YouTubeChannelHandle = "@ExpiredSodaOfficial";

// Optional: Set channel ID directly (auto-resolved if left empty)
public const string YouTubeChannelId = ""; // Leave empty for auto-resolve
```

**To get your Discord channel ID:**
- Enable Developer Mode in Discord (Settings → Advanced → Developer Mode)
- Right-click on #transmissions channel → Copy Channel ID

### 3. Set Environment Variables

Add to Railway (or your local environment):

```bash
YOUTUBE_API_KEY=your_youtube_api_key_here
```

**In Railway:**
1. Go to your project
2. Click on your service
3. Go to "Variables" tab
4. Add new variable: `YOUTUBE_API_KEY` = your key

### 4. Update Bot Permissions

Your bot needs the **Application Commands** scope:

1. Go to [Discord Developer Portal](https://discord.com/developers/applications)
2. Select your bot application
3. Go to "OAuth2" → "URL Generator"
4. Select scopes:
   - ✅ `bot`
   - ✅ `applications.commands`
5. Select permissions:
   - ✅ Manage Roles
   - ✅ Kick Members
   - ✅ Send Messages
   - ✅ Embed Links
   - ✅ Mention Everyone
   - ✅ Read Message History
   - ✅ Use Application Commands
6. Copy the generated URL and re-invite your bot

---

## Usage

### Automatic Announcements

Once configured, the bot will:
1. Check your channel every 10 minutes
2. Detect when you go live
3. Send an announcement to #transmissions with @everyone ping
4. Only announce once per stream (no spam)

### Manual Command

Use `/live` to manually trigger an announcement:

```
/live
```

**Response:**
- ✅ If you're live: "You're live! Announcement sent to all servers."
- ⚠️ If you're not live: "No live stream detected on your channel."

**Note:** Only server administrators can use the `/live` command.

---

## How It Works

### Architecture

```
LiveStreamCheckerService (Background)
    ↓ Every 10 minutes
YouTubeLiveService
    ↓ Calls YouTube API
    ↓ Checks for live streams
LiveStreamAnnouncementService
    ↓ Compares with database
    ↓ If new stream detected
Sends Announcement → #transmissions
    ↓ Updates database
LiveStreamStatus (Database)
```

### Smart Detection

The bot tracks stream state in the database:
- **Video ID** - Unique identifier for each stream
- **Announcement sent** - Flag to prevent duplicates
- **Last checked** - Timestamp of last API call

When a stream ends, the state resets, so the next stream will be announced properly.

### API Usage

**YouTube API Quotas:**
- Each check costs **~100 quota units**
- Default daily quota: **10,000 units**
- 10-minute intervals = **144 checks/day** = **~14,400 units/day**

⚠️ **You may need to request a quota increase** if you have multiple bots or frequent checks.

**To request increase:**
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Navigate to "IAM & Admin" → "Quotas"
3. Search for "YouTube Data API v3"
4. Request increase to 50,000+ units

---

## Configuration Options

### Change Check Interval

In `BotConfig.cs`:
```csharp
public const int LiveCheckIntervalMinutes = 10; // Change to 5, 15, 30, etc.
```

**Note:** Lower intervals = more API usage

### Customize Announcement Message

Edit `LiveStreamAnnouncementService.cs` → `SendAnnouncementAsync()`:

```csharp
var embed = new EmbedBuilder()
    .WithTitle("🔴 YOUR CUSTOM TITLE")
    .WithDescription("Your custom description here!")
    .WithUrl(streamUrl)
    .WithColor(Color.Red)
    // ... customize as needed
    .Build();
```

### Change Who Can Use /live

In `SlashCommandHandler.cs`:

```csharp
// Current: Only admins
.WithDefaultMemberPermissions(GuildPermission.Administrator)

// Option: Specific role
.WithDefaultMemberPermissions(GuildPermission.ManageMessages)

// Option: Everyone (not recommended)
// Remove the .WithDefaultMemberPermissions() line
```

---

## Troubleshooting

### "YouTube API key not set" warning
- Add `YOUTUBE_API_KEY` environment variable to Railway
- Verify the key is valid in Google Cloud Console

### "No live stream detected" (but you're live)
- Check that `YouTubeChannelHandle` is correct in `BotConfig.cs`
- Verify the stream is set to "Public" (not Unlisted/Private)
- Wait up to 10 minutes for the next automatic check
- Or use `/live` to trigger immediately

### "Transmissions channel not found"
- Verify `TransmissionsChannelId` in `BotConfig.cs`
- Make sure the bot has access to view that channel
- Check the configuration validator output on bot startup

### Command not appearing
- Make sure bot has `applications.commands` scope
- Wait a few minutes for Discord to register the command
- Try kicking and re-inviting the bot

### API quota exceeded
- Request a quota increase from Google Cloud Console
- Increase `LiveCheckIntervalMinutes` to reduce API calls
- Check if other applications are using the same API key

---

## Database Schema

### LiveStreamStatus Table

| Column | Type | Description |
|--------|------|-------------|
| Id | int | Primary key |
| Platform | string | Always "YouTube" |
| CurrentVideoId | string | Unique ID of current live stream |
| IsLive | bool | Whether channel is currently live |
| LiveStartedAt | DateTime? | When current stream started |
| LastCheckedAt | DateTime | Last API check timestamp |
| AnnouncementSent | bool | Whether announcement was sent for current stream |

---

## Testing

### Test Locally

```bash
# Set environment variables
$env:DISCORD_BOT_TOKEN="your_token"
$env:YOUTUBE_API_KEY="your_key"
$env:DATABASE_URL="optional_if_testing"

# Run the bot
dotnet run --project CultBot/CultBot.csproj
```

### Test Without Going Live

Temporarily modify `YouTubeLiveService.cs` → `CheckIfLiveAsync()` to return test data:

```csharp
// FOR TESTING ONLY - Remove after testing
return (true, "test-video-id", "https://youtube.com/test");
```

This will trigger announcements without needing an actual live stream.

---

## Example Announcement

```
@everyone

🔴 LIVE NOW ON YOUTUBE

Hey everyone, I'm live on YouTube! Come join the stream and hang out!

Stream Link
[Click here to watch](https://youtube.com/watch?v=abc123)

Today at 3:45 PM
Auto-detected
```

---

## Support

If you encounter issues:
1. Check the bot logs in Railway dashboard
2. Verify all configuration in `BotConfig.cs`
3. Run the configuration validator (happens automatically on startup)
4. Check YouTube API quota usage in Google Cloud Console
