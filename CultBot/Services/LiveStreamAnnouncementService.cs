using CultBot.Configuration;
using CultBot.Data;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CultBot.Services;

public sealed record LiveAnnouncementResult(
    bool IsLive,
    bool AnnouncementAttempted,
    bool AnnouncementSent,
    string? VideoId,
    string? VideoUrl,
    string Message);

public class LiveStreamAnnouncementService
{
    private readonly SemaphoreSlim _announcementLock = new(1, 1);
    private readonly DiscordSocketClient _client;
    private readonly YouTubeLiveService _youtubeService;
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;

    public LiveStreamAnnouncementService(
        DiscordSocketClient client,
        YouTubeLiveService youtubeService,
        IDbContextFactory<CultBotDbContext> contextFactory)
    {
        _client = client;
        _youtubeService = youtubeService;
        _contextFactory = contextFactory;
    }

    public async Task<LiveAnnouncementResult> CheckAndAnnounceAsync(bool isManualTrigger = false)
    {
        await _announcementLock.WaitAsync();

        try
        {
            Console.WriteLine($"Checking YouTube live status... (Manual: {isManualTrigger})");

            // Check if stream is live
            var (isLive, videoId, videoUrl) = await _youtubeService.CheckIfLiveAsync();

            await using var context = await _contextFactory.CreateDbContextAsync();

            // Get or create status record
            var status = await context.LiveStreamStatuses
                .FirstOrDefaultAsync(s => s.Platform == BotConfig.LiveStreamPlatformYouTube);

            if (status == null)
            {
                status = new LiveStreamStatus
                {
                    Platform = BotConfig.LiveStreamPlatformYouTube,
                    LastCheckedAt = DateTime.UtcNow
                };
                context.LiveStreamStatuses.Add(status);
            }

            status.LastCheckedAt = DateTime.UtcNow;

            // Handle live state
            if (isLive && !string.IsNullOrEmpty(videoId) && !string.IsNullOrEmpty(videoUrl))
            {
                var now = DateTime.UtcNow;
                var alreadyAnnouncedThisVideo =
                    string.Equals(status.LastAnnouncedVideoId, videoId, StringComparison.Ordinal) ||
                    (status.LastAnnouncedVideoId == null &&
                        status.AnnouncementSent &&
                        string.Equals(status.CurrentVideoId, videoId, StringComparison.Ordinal));
                var isNewLiveSession = !string.Equals(status.CurrentVideoId, videoId, StringComparison.Ordinal);
                var shouldAnnounce = !alreadyAnnouncedThisVideo;

                status.IsLive = true;
                status.CurrentVideoId = videoId;

                if (isNewLiveSession || !status.LiveStartedAt.HasValue)
                {
                    status.LiveStartedAt = now;
                }

                if (shouldAnnounce)
                {
                    var sent = await SendAnnouncementAsync(videoUrl, isManualTrigger);
                    status.AnnouncementSent = sent;
                    if (sent)
                    {
                        status.LastAnnouncedVideoId = videoId;
                        status.LastAnnouncementSentAt = now;
                        status.AnnouncementSent = true;
                        Console.WriteLine($"✓ Announcement sent for stream: {videoId}");
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: Stream is live, but no announcement was sent for stream: {videoId}");
                    }

                    await context.SaveChangesAsync();

                    var message = sent
                        ? $"✓ You're live! Announcement sent.\nStream: {videoUrl}"
                        : $"⚠️ You're live, but I could not send the Discord announcement. Check channel configuration and bot permissions.\nStream: {videoUrl}";

                    return new LiveAnnouncementResult(true, true, sent, videoId, videoUrl, message);
                }
                else
                {
                    if (status.LastAnnouncedVideoId == null)
                        status.LastAnnouncedVideoId = videoId;
                    status.AnnouncementSent = true;
                    Console.WriteLine("Stream is live but announcement already sent for this stream");
                }

                await context.SaveChangesAsync();
                return new LiveAnnouncementResult(
                    true,
                    false,
                    false,
                    videoId,
                    videoUrl,
                    $"✓ You're live, and this stream was already announced.\nStream: {videoUrl}");
            }
            else
            {
                // Stream is not live
                if (status.IsLive)
                {
                    Console.WriteLine("Stream ended, resetting status");
                }

                status.IsLive = false;
                status.LiveStartedAt = null;
                status.AnnouncementSent = status.CurrentVideoId != null &&
                    string.Equals(status.CurrentVideoId, status.LastAnnouncedVideoId, StringComparison.Ordinal);

                await context.SaveChangesAsync();
                var channelUrl = await _youtubeService.GetChannelUrlAsync();
                return new LiveAnnouncementResult(
                    false,
                    false,
                    false,
                    null,
                    null,
                    $"⚠️ No live stream detected on your channel.\nChannel: {channelUrl ?? BotConfig.YouTubeChannelHandle}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in CheckAndAnnounceAsync: {ex.Message}");
            return new LiveAnnouncementResult(false, false, false, null, null, "An error occurred while checking YouTube live status.");
        }
        finally
        {
            _announcementLock.Release();
        }
    }

    private async Task<bool> SendAnnouncementAsync(string streamUrl, bool isManualTrigger)
    {
        var sentAny = false;

        foreach (var guild in _client.Guilds)
        {
            var transmissionsChannel = guild.GetTextChannel(BotConfig.TransmissionsChannelId);
                
            if (transmissionsChannel == null)
            {
                Console.WriteLine($"WARNING: #transmissions channel not found in guild {guild.Name}");
                continue;
            }

            var embed = new EmbedBuilder()
                .WithTitle("🔴 LIVE NOW ON YOUTUBE")
                .WithDescription("Hey everyone, I'm live on YouTube! Come join the stream and hang out!")
                .WithUrl(streamUrl)
                .WithColor(Color.Red)
                .WithThumbnailUrl("https://www.youtube.com/s/desktop/6e27bc15/img/favicon_144x144.png")
                .AddField("Stream Link", $"[Click here to watch]({streamUrl})", false)
                .WithCurrentTimestamp()
                .WithFooter(isManualTrigger ? "Manually triggered" : "Auto-detected")
                .Build();

            try
            {
                await transmissionsChannel.SendMessageAsync(text: "@everyone", embed: embed);
                sentAny = true;
                Console.WriteLine($"✓ Live announcement sent to #{transmissionsChannel.Name} in {guild.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR sending announcement to #{transmissionsChannel.Name} in {guild.Name}: {ex.Message}");
            }
        }

        return sentAny;
    }

}
