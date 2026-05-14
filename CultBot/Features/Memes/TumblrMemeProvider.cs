using System.Text.Json;
using System.Text.RegularExpressions;
using CultBot.Configuration;

namespace CultBot.Features.Memes;

public class TumblrMemeProvider
{
    private const string SourceName = "Tumblr";
    private readonly HttpClient _httpClient;
    private int _nextTagIndex;
    private bool _loggedMissingConfig;

    private static readonly string[] RiskyTerms =
    {
        "nsfw",
        "adult",
        "porn",
        "gore",
        "police shooting",
        "murder",
        "killed",
        "rip",
        "george floyd",
        "breonna",
        "politics",
        "election",
        "war"
    };

    public TumblrMemeProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BotConfig.TumblrConsumerKeyEnvironmentVariable));

    public async Task<MemeFetchResult> GetCandidatesAsync(
        IReadOnlySet<string> excludedSourcePostIds,
        CancellationToken cancellationToken)
    {
        var apiKey = Environment.GetEnvironmentVariable(BotConfig.TumblrConsumerKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (!_loggedMissingConfig)
            {
                Console.WriteLine("TumblrMemeProvider disabled: TUMBLR_CONSUMER_KEY is missing.");
                _loggedMissingConfig = true;
            }

            return new MemeFetchResult(
                MemeFetchStatus.NotConfigured,
                Array.Empty<MemeCandidate>(),
                "TUMBLR_CONSUMER_KEY is missing.");
        }

        var candidates = new List<MemeCandidate>();
        var sawSuccessfulFetch = false;
        var sawFailure = false;

        foreach (var tag in GetTagsInRotation())
        {
            var tagResult = await FetchTagAsync(apiKey, tag, excludedSourcePostIds, cancellationToken);
            if (tagResult.Status == MemeFetchStatus.RateLimited)
                return tagResult;

            if (tagResult.Status != MemeFetchStatus.Success)
            {
                sawFailure = true;
                continue;
            }

            sawSuccessfulFetch = true;
            candidates.AddRange(tagResult.Candidates);
        }

        if (candidates.Count > 0 || sawSuccessfulFetch)
            return MemeFetchResult.Success(candidates);

        return new MemeFetchResult(
            MemeFetchStatus.Failed,
            Array.Empty<MemeCandidate>(),
            sawFailure ? "Tumblr tag fetches failed." : "No Tumblr tags were configured.");
    }

    private IEnumerable<string> GetTagsInRotation()
    {
        var tags = BotConfig.TumblrMemeTags;
        if (tags.Length == 0)
            yield break;

        var start = (Interlocked.Increment(ref _nextTagIndex) & int.MaxValue) % tags.Length;
        for (var i = 0; i < tags.Length; i++)
        {
            yield return tags[(start + i) % tags.Length];
        }
    }

    private async Task<MemeFetchResult> FetchTagAsync(
        string apiKey,
        string tag,
        IReadOnlySet<string> excludedSourcePostIds,
        CancellationToken cancellationToken)
    {
        var url = "https://api.tumblr.com/v2/tagged" +
            $"?tag={Uri.EscapeDataString(tag)}" +
            $"&limit={BotConfig.TumblrFetchLimit}" +
            "&filter=text" +
            "&npf=true" +
            $"&api_key={Uri.EscapeDataString(apiKey)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddUserAgent(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            Console.WriteLine($"Tumblr rate limit hit while fetching tag '{tag}'.");
            return new MemeFetchResult(MemeFetchStatus.RateLimited, Array.Empty<MemeCandidate>(), "Tumblr rate limit hit.");
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Tumblr meme fetch failed for tag '{tag}': {(int)response.StatusCode} {response.ReasonPhrase}");
            return new MemeFetchResult(MemeFetchStatus.Failed, Array.Empty<MemeCandidate>(), response.ReasonPhrase);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("response", out var posts) ||
            posts.ValueKind != JsonValueKind.Array)
        {
            return new MemeFetchResult(
                MemeFetchStatus.Failed,
                Array.Empty<MemeCandidate>(),
                "Tumblr response did not include tagged posts.");
        }

        var candidates = new List<MemeCandidate>();
        foreach (var post in posts.EnumerateArray())
        {
            if (TryCreateCandidate(post, excludedSourcePostIds, out var candidate))
                candidates.Add(candidate);
        }

        return MemeFetchResult.Success(candidates);
    }

    private static bool TryCreateCandidate(
        JsonElement post,
        IReadOnlySet<string> excludedSourcePostIds,
        out MemeCandidate candidate)
    {
        candidate = null!;

        var postId = GetString(post, "id_string") ?? GetNumberAsString(post, "id");
        if (string.IsNullOrWhiteSpace(postId) ||
            excludedSourcePostIds.Contains(postId))
        {
            return false;
        }

        var type = GetString(post, "type");
        if (type is "video" or "audio" or "link" or "quote" or "answer" or "chat")
            return false;

        if (HasMatureSignal(post) || ContainsRiskyTerms(post))
            return false;

        if (!TryGetSingleStaticImageUrl(post, out var imageUrl, out var extension))
            return false;

        var permalink = GetString(post, "post_url") ?? string.Empty;
        candidate = new MemeCandidate(SourceName, postId, imageUrl, extension, permalink);
        return true;
    }

    private static bool TryGetSingleStaticImageUrl(JsonElement post, out string imageUrl, out string extension)
    {
        imageUrl = string.Empty;
        extension = string.Empty;

        if (post.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            var imageBlocks = new List<JsonElement>();
            foreach (var block in content.EnumerateArray())
            {
                var blockType = GetString(block, "type");
                if (blockType is "video" or "audio" or "link")
                    return false;

                if (blockType == "image")
                    imageBlocks.Add(block);
            }

            if (imageBlocks.Count > 0)
            {
                if (imageBlocks.Count != 1 ||
                    !TryGetBestNpfImageUrl(imageBlocks[0], out imageUrl))
                {
                    return false;
                }

                return TryGetStaticImageExtension(imageUrl, out extension);
            }
        }

        if (post.TryGetProperty("photos", out var photos) &&
            photos.ValueKind == JsonValueKind.Array)
        {
            if (photos.GetArrayLength() != 1)
                return false;

            var photo = photos.EnumerateArray().First();
            if (!TryGetBestLegacyPhotoUrl(photo, out imageUrl))
                return false;

            return TryGetStaticImageExtension(imageUrl, out extension);
        }

        return false;
    }

    private static bool TryGetBestLegacyPhotoUrl(JsonElement photo, out string url)
    {
        url = string.Empty;
        var bestArea = -1;

        if (photo.TryGetProperty("original_size", out var original))
            TryPromoteMediaUrl(original, ref url, ref bestArea);

        if (photo.TryGetProperty("alt_sizes", out var altSizes) &&
            altSizes.ValueKind == JsonValueKind.Array)
        {
            foreach (var altSize in altSizes.EnumerateArray())
                TryPromoteMediaUrl(altSize, ref url, ref bestArea);
        }

        return !string.IsNullOrWhiteSpace(url);
    }

    private static bool TryGetBestNpfImageUrl(JsonElement imageBlock, out string url)
    {
        url = string.Empty;
        var bestArea = -1;

        if (!imageBlock.TryGetProperty("media", out var media))
            return false;

        if (media.ValueKind == JsonValueKind.Array)
        {
            foreach (var mediaItem in media.EnumerateArray())
                TryPromoteMediaUrl(mediaItem, ref url, ref bestArea);
        }
        else if (media.ValueKind == JsonValueKind.Object)
        {
            TryPromoteMediaUrl(media, ref url, ref bestArea);
        }

        return !string.IsNullOrWhiteSpace(url);
    }

    private static void TryPromoteMediaUrl(JsonElement media, ref string bestUrl, ref int bestArea)
    {
        var url = GetString(media, "url");
        if (string.IsNullOrWhiteSpace(url) ||
            !TryGetStaticImageExtension(url, out _))
        {
            return;
        }

        var width = GetInt(media, "width");
        var height = GetInt(media, "height");
        var area = width > 0 && height > 0 ? width * height : 0;
        if (area >= bestArea)
        {
            bestUrl = url;
            bestArea = area;
        }
    }

    private static bool HasMatureSignal(JsonElement post)
    {
        if (GetBool(post, "is_nsfw") ||
            GetBool(post, "is_mature") ||
            GetBool(post, "is_adult") ||
            GetBool(post, "adult") ||
            GetBool(post, "sensitive") ||
            GetBool(post, "contains_sensitive_media"))
        {
            return true;
        }

        var rating = GetString(post, "content_rating") ??
            GetString(post, "contentRating") ??
            GetString(post, "rating");
        return rating != null &&
            (rating.Contains("mature", StringComparison.OrdinalIgnoreCase) ||
                rating.Contains("adult", StringComparison.OrdinalIgnoreCase) ||
                rating.Contains("explicit", StringComparison.OrdinalIgnoreCase) ||
                rating.Contains("nsfw", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsRiskyTerms(JsonElement post)
    {
        var values = new List<string>();
        AddStringIfPresent(post, values, "summary");
        AddStringIfPresent(post, values, "caption");
        AddStringIfPresent(post, values, "body");
        AddStringIfPresent(post, values, "title");

        if (post.TryGetProperty("tags", out var tags) &&
            tags.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tags.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String)
                    values.Add(tag.GetString() ?? string.Empty);
            }
        }

        if (post.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
                AddStringIfPresent(block, values, "text");
        }

        var text = StripHtml(string.Join(' ', values)).ToLowerInvariant();
        foreach (var term in RiskyTerms)
        {
            if (term.Contains(' ', StringComparison.Ordinal))
            {
                if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return true;
                continue;
            }

            if (Regex.IsMatch(text, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    private static void AddStringIfPresent(JsonElement element, List<string> values, string propertyName)
    {
        var value = GetString(element, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value);
    }

    private static string StripHtml(string value)
    {
        return Regex.Replace(value, "<.*?>", " ");
    }

    private static bool TryGetStaticImageExtension(string url, out string extension)
    {
        extension = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        var path = uri.AbsolutePath;
        var dotIndex = path.LastIndexOf('.');
        if (dotIndex < 0)
            return false;

        extension = path[(dotIndex + 1)..].ToLowerInvariant();
        return extension is "jpg" or "jpeg" or "png";
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetNumberAsString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var id) => id.ToString(),
            JsonValueKind.String => value.GetString(),
            _ => null
        };
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            value.GetBoolean();
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number)
                ? number
                : 0;
    }

    private static void AddUserAgent(HttpRequestMessage request)
    {
        request.Headers.UserAgent.TryParseAdd("ExpiredSodaCultBot/1.0");
    }
}
