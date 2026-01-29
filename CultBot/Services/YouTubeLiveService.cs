using CultBot.Configuration;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;

namespace CultBot.Services;

public class YouTubeLiveService
{
    private readonly Google.Apis.YouTube.v3.YouTubeService _youtubeService;
    private string? _channelId;

    public YouTubeLiveService()
    {
        var apiKey = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("WARNING: YOUTUBE_API_KEY environment variable not set!");
            Console.WriteLine("YouTube live checking will not work. Get a key from: https://console.cloud.google.com/");
        }

        _youtubeService = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer()
        {
            ApiKey = apiKey,
            ApplicationName = "CultBot"
        });
    }

    public async Task<(bool isLive, string? videoId, string? videoUrl)> CheckIfLiveAsync()
    {
        try
        {
            // Get channel ID if we don't have it yet
            if (string.IsNullOrEmpty(_channelId))
            {
                _channelId = await ResolveChannelIdAsync();
                if (string.IsNullOrEmpty(_channelId))
                {
                    Console.WriteLine("ERROR: Could not resolve YouTube channel ID");
                    return (false, null, null);
                }
            }

            // Search for live broadcasts on this channel
            var searchRequest = _youtubeService.Search.List("snippet");
            searchRequest.ChannelId = _channelId;
            searchRequest.EventType = SearchResource.ListRequest.EventTypeEnum.Live;
            searchRequest.Type = "video";
            searchRequest.MaxResults = 1;

            var searchResponse = await searchRequest.ExecuteAsync();

            if (searchResponse.Items != null && searchResponse.Items.Count > 0)
            {
                var liveVideo = searchResponse.Items[0];
                var videoId = liveVideo.Id.VideoId;
                var videoUrl = $"https://www.youtube.com/watch?v={videoId}";

                Console.WriteLine($"✓ YouTube: Found live stream - {liveVideo.Snippet.Title}");
                return (true, videoId, videoUrl);
            }

            Console.WriteLine("YouTube: No live stream detected");
            return (false, null, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR checking YouTube live status: {ex.Message}");
            return (false, null, null);
        }
    }

    private async Task<string?> ResolveChannelIdAsync()
    {
        try
        {
            // If channel ID is already set in config, use it
            if (!string.IsNullOrEmpty(BotConfig.YouTubeChannelId))
            {
                Console.WriteLine($"Using configured YouTube Channel ID: {BotConfig.YouTubeChannelId}");
                return BotConfig.YouTubeChannelId;
            }

            // Otherwise, resolve from handle
            var handle = BotConfig.YouTubeChannelHandle.TrimStart('@');
            Console.WriteLine($"Resolving YouTube channel ID for handle: @{handle}");

            // Search for the channel by custom URL/handle
            var searchRequest = _youtubeService.Search.List("snippet");
            searchRequest.Q = handle;
            searchRequest.Type = "channel";
            searchRequest.MaxResults = 1;

            var searchResponse = await searchRequest.ExecuteAsync();

            if (searchResponse.Items != null && searchResponse.Items.Count > 0)
            {
                var channelId = searchResponse.Items[0].Snippet.ChannelId;
                Console.WriteLine($"✓ Resolved YouTube Channel ID: {channelId}");
                return channelId;
            }

            Console.WriteLine($"ERROR: Could not find YouTube channel for handle @{handle}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR resolving YouTube channel ID: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> GetChannelUrlAsync()
    {
        if (string.IsNullOrEmpty(_channelId))
        {
            _channelId = await ResolveChannelIdAsync();
        }

        if (!string.IsNullOrEmpty(_channelId))
        {
            return $"https://www.youtube.com/{BotConfig.YouTubeChannelHandle}";
        }

        return null;
    }
}
