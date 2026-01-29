# ExpiredSodaCultBot – Setup Guide

This guide covers deployment, cult initiation, YouTube live announcements, moderation, and database setup in one place.

---

## 1. Before Deploying to Railway

### 1.1 Discord Developer Portal

https://discord.com/developers/applications

**Enable Privileged Intents (Bot → Privileged Gateway Intents):**
- **PRESENCE INTENT** – game tracking
- **SERVER MEMBERS INTENT** – join/leave tracking
- **MESSAGE CONTENT INTENT** – spam/profanity detection

**Bot Permissions (invite with):**
- Manage Roles, Kick Members, Ban Members, Manage Messages
- Send Messages, View Channels, Read Message History

Permission integer: `268445718` (use https://discordapi.com/permissions.html to customize).

### 1.2 Railway Environment Variables

In your Railway project:

- `DISCORD_BOT_TOKEN` – your Discord bot token
- `DATABASE_URL` – auto-configured when you add PostgreSQL
- `YOUTUBE_API_KEY` – for YouTube live checks (see section 3)

### 1.3 Discord Server Setup

**Channels:** #gateway, #role-ritual, #transmissions, #mod-logs  
**Roles:** The Uninitiated, Silent Witness, Neon Disciple, Veiled Archivist

Enable Developer Mode (Settings → Advanced), then right-click channels/roles → Copy ID.

### 1.4 BotConfig.cs

Open `CultBot/Configuration/BotConfig.cs` and set your IDs:

- **Channels:** GatewayChannelId, RoleRitualChannelId, TransmissionsChannelId, ModLogChannelId
- **Roles:** TheUninitiatedRoleId, SilentWitnessRoleId, NeonDiscipleRoleId, VeiledArchivistRoleId
- **GIF URLs:** SilentWitnessGifUrl, NeonDiscipleGifUrl, VeiledArchivistGifUrl (for initiation success messages)

---

## 2. Cult Initiation System

- New members get **The Uninitiated** role and a welcome message in #gateway.
- They must complete the **Rite of Choosing** in #role-ritual (button selection) within **24 hours**.
- If they don’t, they are kicked and the session is marked expired.
- **Recovery:** If the bot was down when someone joined, a periodic check finds users with The Uninitiated role but no initiation session and sends them the ritual message (24h starts from then). Configure `RecoveryMaxJoinAgeDays` in BotConfig (0 = no limit; e.g. 7 = only users who joined in the last 7 days).

**#role-ritual permissions:** Allow The Uninitiated (Read, View Channel, Read Message History). Deny @everyone. Other channels: deny The Uninitiated; allow the three path roles.

**Timeouts:** `InitiationTimeoutHours` (default 24), `ExpirationCheckIntervalMinutes` (default 5).

---

## 3. YouTube Live Announcements

- Bot checks your YouTube channel for live streams and announces in #transmissions with @everyone.
- **Schedule:** Checks only between **8pm–5am EST** (configurable: `LiveCheckWindowStartHour`, `LiveCheckWindowEndHour`, `LiveCheckTimezoneId` in BotConfig).
- **When already live:** After an announcement is sent, the bot checks less often (`AlreadyLiveCheckIntervalMinutes`, default 30) to reduce API usage.
- **Manual:** `/live` (admin only) triggers an immediate check and announcement.

**YouTube API key:**
1. [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Library → enable **YouTube Data API v3**.
2. Credentials → Create Credentials → API Key. Add `YOUTUBE_API_KEY` to Railway.

**BotConfig:** TransmissionsChannelId, YouTubeChannelHandle (e.g. `@expiredsodaofficial`), YouTubeChannelId (optional; leave empty to resolve from handle). LiveCheckIntervalMinutes (default 10), AlreadyLiveCheckIntervalMinutes (default 30).

**Bot invite:** Include `applications.commands` scope for `/live`.

---

## 4. Moderation & Data Collection

**Intents:** MESSAGE CONTENT, SERVER MEMBERS, PRESENCE (see 1.1).

**BotConfig:**
- **ModLogChannelId** – optional mod log channel
- **Spam:** SpamMessageThreshold (5), SpamTimeWindowSeconds (10), SpamScoreThreshold (15), SlowModeDurationMinutes (5), BotBanThreshold (25)
- **Profanity:** RacialSlurs (string array; add terms as needed)
- **Game tracking:** TrackedGames (array of game names)

Spam/profanity messages are deleted; slow mode and bans are applied per configured thresholds. Game activity and join/leave are stored for stats.

---

## 5. Database

**Railway:** Add PostgreSQL to the project. The bot uses `EnsureCreatedAsync()` on startup; no manual migrations needed.

**Local:** If `DATABASE_URL` is not set, the bot uses an in-memory database (data is lost on restart).

**Optional EF migrations:** Install `dotnet-ef`, then from CultBot: `dotnet ef migrations add InitialCreate --output-dir Data/Migrations`, `dotnet ef database update`. To use migrations instead of EnsureCreated, in BotService.StartAsync replace `EnsureCreatedAsync` with `MigrateAsync`.

**Railway DB access:** Project → PostgreSQL → Connect; use the provided URL with pgAdmin/DBeaver or `railway connect`.

---

## 6. Verify Deployment

- Railway logs: “Database initialized.”, “Cult Bot is ready!”, configuration validator ✓ for channels/roles.
- Test: join with a test account (role + ritual message), click a role button, run `/live`, send spam to test moderation.

---

## 7. Troubleshooting

- **Bot offline:** Check DISCORD_BOT_TOKEN and intents.
- **Role/channel not found:** Verify IDs in BotConfig.
- **YouTube:** Ensure YOUTUBE_API_KEY is set; check quota in Google Cloud Console. Live checks run only in the configured time window.
- **Recovery:** Users who missed initiation get the ritual on the next expiration-cycle run (every few minutes) if they have The Uninitiated role and no pending session (and, if set, joined within RecoveryMaxJoinAgeDays).

For more detail on any section, see the project README and code comments in `CultBot/Configuration/BotConfig.cs` and the relevant services.
