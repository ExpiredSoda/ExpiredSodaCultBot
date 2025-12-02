# Moderation & Data Collection Setup Guide

## Overview
The ExpiredSodaCultBot includes comprehensive moderation and user activity tracking features:

### Features
- **Spam Detection**: Automatic detection and punishment for spam patterns
- **Bot Detection**: Identifies and bans suspicious bot accounts
- **Profanity Filter**: Detects and removes messages with racial slurs
- **User Activity Tracking**: Logs messages, games played, and join/leave events
- **Slow Mode**: Temporary message rate limiting for spam offenders
- **Automatic Escalation**: Progressive punishments (warning → slow mode → ban)

## Required Discord Intents
The bot requires these privileged intents (configured in Discord Developer Portal):
- ✅ **MESSAGE CONTENT** - Required to scan messages for spam/profanity
- ✅ **SERVER MEMBERS** - Required to track joins/leaves
- ✅ **PRESENCE** - Required to track game activity

⚠️ Enable these in: https://discord.com/developers/applications → Your Bot → Bot → Privileged Gateway Intents

## Configuration (BotConfig.cs)

### Channel IDs
```csharp
public static readonly ulong ModLogChannelId = 0; // Set this to your mod log channel ID
```

To get channel ID:
1. Enable Developer Mode in Discord (Settings → Advanced → Developer Mode)
2. Right-click your mod log channel → Copy Channel ID
3. Paste the ID into `BotConfig.cs`

### Spam Detection Settings
```csharp
public static readonly int SpamMessageThreshold = 5;        // Messages before flagged as spam
public static readonly int SpamTimeWindowSeconds = 10;      // Time window for spam detection
public static readonly int BotBanThreshold = 25;            // Spam score to trigger auto-ban
```

**Default Behavior:**
- 5+ messages in 10 seconds = spam detection triggered
- Spam score ≥ 25 + bot indicators = automatic ban
- Spam score ≥ 15 = warning + 10 minute slow mode
- Slow mode prevents users from sending messages temporarily

### Profanity Filter
```csharp
public static readonly string[] RacialSlurs = Array.Empty<string>();
```

**To configure:**
1. Edit `BotConfig.cs`
2. Add terms to the array (case-insensitive):
   ```csharp
   public static readonly string[] RacialSlurs = new[]
   {
       "slur1",
       "slur2",
       // Add more terms here
   };
   ```

**Features:**
- Word boundary matching (won't trigger on partial matches)
- Obfuscation detection (handles l33t speak: `a→@`, `e→3`, `i→1`, `o→0`, `s→$`)
- Automatic message deletion
- Three-strike system:
  - 1st offense: Warning
  - 2nd offense: Warning + 30 minute slow mode
  - 3rd offense: Permanent ban

### Game Tracking
```csharp
public static readonly string[] TrackedGames = new[]
{
    "Halo",
    "Destiny",
    "Call of Duty"
};
```

**Tracks:**
- Active play sessions with duration
- Game mentions in messages (tagged as `[Mentioned] GameName`)
- Most played games per user

## Database Schema

### Tables Created
- **UserMessages** - Every message sent with content, timestamps, flags
- **UserActivities** - User stats (message counts, warnings, current game, join/leave times)
- **GameActivities** - Game play sessions with start/end times and duration
- **ModerationLogs** - All moderation actions (warnings, bans, slow mode)
- **SpamTrackers** - Spam detection state per user

### Data Collected
- Total message count per user
- Warning count and slow mode applications
- Join/leave count and timestamps
- Game play duration and frequency
- All moderation actions with reasons

## Bot Permissions Required
The bot needs these Discord permissions:
- ✅ Manage Roles (for The Uninitiated role)
- ✅ Kick Members (for initiation expiration)
- ✅ Ban Members (for moderation)
- ✅ Manage Messages (to delete spam/profanity)
- ✅ Send Messages
- ✅ View Channels
- ✅ Read Message History

## Testing

### Test Spam Detection
1. Send 5+ messages rapidly in 10 seconds
2. Should trigger spam warning and slow mode
3. While in slow mode, messages are auto-deleted

### Test Profanity Filter
1. Add a test word to `RacialSlurs` array
2. Send message containing the word
3. Message should be deleted immediately
4. Receive warning via DM
5. Repeated offenses trigger slow mode → ban

### Test Bot Detection
Create a test account with:
- New account (< 7 days old)
- Messages with lots of links
- No avatar
- Numeric username

Should be flagged as suspected bot with high spam score.

### Test Game Tracking
1. Start playing a tracked game in Discord
2. Bot logs game session to database
3. Stop playing → bot calculates duration
4. Check logs: `🎮 {Username} started playing: {Game}`

## Monitoring

### Console Output
The bot logs all moderation actions:
```
⚠️ {Username} flagged for spam (Score: 20)
🚫 {Username} profanity detected: [term] (Offense #2)
📊 Tracked join: {Username} (Total joins: 1)
🎮 {Username} started playing: Halo
```

### Mod Log Channel
If configured, all actions are logged to the mod channel:
- Warnings with reason
- Slow mode applications
- Bans with automated/manual flag

## Privacy Considerations
⚠️ The bot stores:
- All message content in the database
- User join/leave timestamps
- Game playing history

Ensure compliance with:
- Discord Terms of Service
- Your server's privacy policy
- Applicable data protection laws (GDPR, etc.)

## Troubleshooting

### Messages not being scanned
- Check MESSAGE CONTENT intent is enabled
- Verify bot has "View Channel" permission
- Check console for errors

### Game tracking not working
- Verify PRESENCE intent is enabled
- User must have "Display current activity as status message" enabled
- Only tracks ActivityType.Playing (not streaming/listening)

### Slow mode not applying
- Check bot has "Manage Messages" permission
- User must be in the UserActivity database (send at least one message first)
- Slow mode duration stored in SpamTracker table

### Database errors
- Ensure PostgreSQL connection string is valid
- Run `dotnet ef database update` to apply migrations
- Check Railway logs for connection issues

## Advanced: Querying User Stats

Add a slash command to expose user stats:
```csharp
// In SlashCommandHandler.cs
var stats = await _dataCollectionService.GetUserStatsAsync(userId, guildId);
// Returns: username, totalMessages, warnings, topGames, etc.
```

## Support
For issues or feature requests, check the GitHub repository or contact the bot maintainer.
