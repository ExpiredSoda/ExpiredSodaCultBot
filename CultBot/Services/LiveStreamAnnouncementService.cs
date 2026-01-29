using CultBot.Configuration;
using CultBot.Data;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CultBot.Services;

public class LiveStreamAnnouncementService
{
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

    public async Task<bool> CheckAndAnnounceAsync(bool isManualTrigger = false)
    {
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
                // Check if this is a new stream or if we haven't announced it yet
                bool shouldAnnounce = isManualTrigger || 
                                     status.CurrentVideoId != videoId || 
                                     !status.AnnouncementSent;

                status.IsLive = true;
                status.CurrentVideoId = videoId;

                if (!status.LiveStartedAt.HasValue)
                {
                    status.LiveStartedAt = DateTime.UtcNow;
                }

                if (shouldAnnounce)
                {
                    await SendAnnouncementAsync(videoUrl, isManualTrigger);
                    status.AnnouncementSent = true;
                    Console.WriteLine($"✓ Announcement sent for stream: {videoId}");
                }
                else
                {
                    Console.WriteLine("Stream is live but announcement already sent for this stream");
                }

                await context.SaveChangesAsync();
                return true;
            }
            else
            {
                // Stream is not live
                if (status.IsLive)
                {
                    Console.WriteLine("Stream ended, resetting status");
                }

                status.IsLive = false;
                status.CurrentVideoId = null;
                status.LiveStartedAt = null;
                status.AnnouncementSent = false;

                await context.SaveChangesAsync();
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in CheckAndAnnounceAsync: {ex.Message}");
            return false;
        }
    }

    private async Task SendAnnouncementAsync(string streamUrl, bool isManualTrigger)
    {
        try
        {
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

                var message = await transmissionsChannel.SendMessageAsync(
                    text: "@everyone",
                    embed: embed
                );

                Console.WriteLine($"✓ Live announcement sent to #{transmissionsChannel.Name} in {guild.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR sending announcement: {ex.Message}");
        }
    }

    public async Task<string> GetManualAnnouncementResponseAsync()
    {
        var (isLive, videoId, videoUrl) = await _youtubeService.CheckIfLiveAsync();

        if (isLive && !string.IsNullOrEmpty(videoUrl))
        {
            return $"✓ You're live! Announcement sent to all servers.\nStream: {videoUrl}";
        }
        else
        {
            var channelUrl = await _youtubeService.GetChannelUrlAsync();
            return $"⚠️ No live stream detected on your channel.\nChannel: {channelUrl ?? BotConfig.YouTubeChannelHandle}";
        }
    }
}
