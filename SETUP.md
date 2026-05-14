# ExpiredSodaCultBot – Setup Guide

This guide covers deployment, cult initiation, YouTube live announcements, Tumblr image memes, moderation, and database setup in one place.

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
- Send Messages, View Channels, Read Message History, Attach Files

Permission integer: `268445718` (use https://discordapi.com/permissions.html to customize).

### 1.2 Railway Environment Variables

In your Railway project:

- `DISCORD_BOT_TOKEN` – your Discord bot token
- `DATABASE_URL` – auto-configured when you add PostgreSQL
- `YOUTUBE_API_KEY` – for YouTube live checks (see section 3)
- `TUMBLR_CONSUMER_KEY` – Tumblr OAuth consumer key for image memes

### 1.3 Discord Server Setup

**Channels:** #gateway, #role-ritual, #transmissions, #memes, #mod-logs
**Roles:** The Uninitiated, Silent Witness, Neon Disciple, Veiled Archivist

Enable Developer Mode (Settings → Advanced), then right-click channels/roles → Copy ID.

### 1.4 BotConfig.cs

Open `CultBot/Configuration/BotConfig.cs` and set your IDs:

- **Channels:** GatewayChannelId, RoleRitualChannelId, TransmissionsChannelId, MemesChannelId, ModLogChannelId
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
- **Schedule:** Checks all day so streams can be detected at any time.
- **Quota:** Automatic checks run every `LiveCheckIntervalMinutes` (default 20). The bot uses YouTube `search.list`, which costs 100 quota units per check, so 20 minutes stays under the default daily quota with room for manual checks.
- **When already live:** After an announcement is sent, the bot checks less often (`AlreadyLiveCheckIntervalMinutes`, default 60) to reduce API usage during long streams.
- **Duplicates:** The bot remembers the last announced YouTube video ID and will not announce the same stream again, even if a temporary YouTube miss makes the stream look offline for one check.
- **Manual:** `/live` (admin only) triggers an immediate status check. It reports when the current stream was already announced instead of forcing a duplicate announcement.

**YouTube API key:**
1. [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Library → enable **YouTube Data API v3**.
2. Credentials → Create Credentials → API Key. Add `YOUTUBE_API_KEY` to Railway.

**BotConfig:** TransmissionsChannelId, YouTubeChannelHandle (e.g. `@expiredsodaofficial`), YouTubeChannelId (optional; leave empty to resolve from handle). LiveCheckIntervalMinutes (default 20), AlreadyLiveCheckIntervalMinutes (default 60).

**Bot invite:** Include `applications.commands` scope for `/live`.

---

## 4. Tumblr Image Meme Scheduler

- Bot posts one static image meme in #memes at **9:00 AM**, **2:00 PM**, and **8:00 PM** America/New_York.
- Source is Tumblr API v2 `/tagged` using rotating tags: `gamer memes`, `videogame memes`, `video game humor`, `gaming memes`, `black twitter memes`, `black people twitter memes`, and `black memes funny`.
- Posts are image-only Discord file uploads: no Tumblr link text, videos, photosets, GIFs, mature posts, risky-topic posts, or regular selfie/model/photo-shoot posts.
- Duplicate prevention is stored in the `PostedMemes` table by source post ID, image hash, and scheduled slot.
- Public: `/meme` lets initiated members request one meme on demand. Each user gets 3 successful requests per Eastern calendar day with a 10-minute cooldown between attempts.
- Manual: `/meme-now` (admin only) bypasses user limits and fetches one meme immediately using the same filters.

**Tumblr app setup:**
1. Go to https://www.tumblr.com/oauth/apps while logged into Tumblr.
2. Register an app for the bot.
3. Copy the app's OAuth Consumer Key.
4. Add it to Railway as `TUMBLR_CONSUMER_KEY`.

**BotConfig:** `MemesChannelId` (set to your #memes channel ID), `TumblrMemeTags`, `TumblrFetchLimit` (20), `MemePostTimesEastern` (9a/2p/8p), `MemeDailyUserRequestLimit` (3), `MemeUserRequestCooldownMinutes` (10), `MemeMaxImageBytes` (8 MB).

**Bot invite:** Include `applications.commands` scope for `/meme` and `/meme-now`.

---

## 5. Moderation & Data Collection

**Intents:** MESSAGE CONTENT, SERVER MEMBERS, PRESENCE (see 1.1).

**BotConfig:**
- **ModLogChannelId** – optional mod log channel
- **Spam:** SpamMessageThreshold (5), SpamTimeWindowSeconds (10), SpamScoreThreshold (15), SlowModeDurationMinutes (5), BotBanThreshold (25)
- **Profanity:** RacialSlurs (string array; add terms as needed)
- **Game tracking:** TrackedGames (array of game names)

Spam/profanity messages are deleted; slow mode and bans are applied per configured thresholds. Game activity and join/leave are stored for stats.

---

## 6. Giveaway (🎁 | giveaways)

- **Progress:** The bot posts/edits a single progress message in the giveaway channel. It **only updates when the count changes** (no repeat of the same number). Every **7 days** it also sends a **weekly update** message regardless of count.
- **Goal reached:** When initiated member count hits the goal (e.g. 100), the bot posts a message with a **"Draw winners"** button. Only the host (**expiredsoda94**, Discord username) can press it; others get an ephemeral message.
- **Draw:** When the host presses the button, the message **cycles through random initiated members** for a few seconds, then **picks 3 winners** (1st, 2nd, 3rd) and announces them. Prizes: 1st = $60 gift card, 2nd = Discord Nitro, 3rd = custom role (configurable in BotConfig).

**BotConfig:** GiveawayChannelId (set to your 🎁 channel ID), MemberGoal (100), NextGoalAfterGiveaway (200), GiveawayPrize1/2/3, GiveawayHostUsername ("expiredsoda94"), WeeklyUpdateIntervalDays (7), GiveawayCycleDurationSeconds (8).

---

## 7. Database

**Railway:** Add PostgreSQL to the project. The bot uses `EnsureCreatedAsync()` on startup; no manual migrations needed.

**Existing DB (reminder grace period):** If you had a database before the "24h grace after reminder" feature, add the column once:
`ALTER TABLE "InitiationSessions" ADD "ReminderSentAt" timestamp with time zone NULL;`

**Existing DB (giveaway):** If you already had GiveawayStates before the new logic (repeat-count skip, weekly update, draw button), add:
`ALTER TABLE "GiveawayStates" ADD "LastAnnouncedCount" integer NULL, ADD "LastWeeklyUpdateAt" timestamp with time zone NULL, ADD "PendingDrawMessageId" bigint NULL;`

**Existing DB (live de-duplication):** If you already had LiveStreamStatuses before the "announce once per YouTube video" feature, add:
`ALTER TABLE "LiveStreamStatuses" ADD "LastAnnouncedVideoId" text NULL, ADD "LastAnnouncementSentAt" timestamp with time zone NULL;`

**Existing DB (memes):** The bot creates the `PostedMemes` and `MemeRequestUsages` tables automatically on startup if they do not exist. If an older `PostedMemes` table has `RedditPostId`, startup copies that value into the source-neutral `SourcePostId` column automatically.

**Local:** If `DATABASE_URL` is not set, the bot uses an in-memory database (data is lost on restart).

**Optional EF migrations:** Install `dotnet-ef`, then from CultBot: `dotnet ef migrations add InitialCreate --output-dir Data/Migrations`, `dotnet ef database update`. To use migrations instead of EnsureCreated, in BotService.StartAsync replace `EnsureCreatedAsync` with `MigrateAsync`.

**Railway DB access:** Project → PostgreSQL → Connect; use the provided URL with pgAdmin/DBeaver or `railway connect`.

---

## 8. Verify Deployment

- Railway logs: “Database initialized.”, “Cult Bot is ready!”, configuration validator ✓ for channels/roles.
- Test: join with a test account (role + ritual message), click a role button, run `/live`, run `/meme`, run `/meme-now`, send spam to test moderation.

---

## 9. Troubleshooting

- **Bot offline:** Check DISCORD_BOT_TOKEN and intents.
- **Role/channel not found:** Verify IDs in BotConfig.
- **YouTube:** Ensure YOUTUBE_API_KEY is set; check quota in Google Cloud Console. Live checks run all day; increase `LiveCheckIntervalMinutes` if you need to reduce quota usage further.
- **Memes disabled:** Set `MemesChannelId` and `TUMBLR_CONSUMER_KEY`. The bot needs View Channel, Send Messages, and Attach Files in #memes.
- **No meme posted:** The Tumblr tags may not have an unused safe static image right now; the bot skips videos, GIFs, photosets, multiple-image posts, mature posts, risky terms, links, regular portrait/model/photo-shoot posts, posts without meme/humor signals, and duplicates.
- **Meme request blocked:** `/meme` requires one of the initiated path roles and enforces 3 successful requests per user per Eastern day plus a 10-minute cooldown.
- **Recovery:** Users who missed initiation get the ritual on the next expiration-cycle run (every few minutes) if they have The Uninitiated role and no pending session (and, if set, joined within RecoveryMaxJoinAgeDays).

For more detail on any section, see the project README and code comments in `CultBot/Configuration/BotConfig.cs` and the relevant services.
