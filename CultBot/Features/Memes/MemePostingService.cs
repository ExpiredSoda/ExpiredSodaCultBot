using System.Security.Cryptography;
using CultBot.Configuration;
using CultBot.Data;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CultBot.Features.Memes;

public class MemePostingService
{
    private readonly SemaphoreSlim _postLock = new(1, 1);
    private readonly DiscordSocketClient _client;
    private readonly TumblrMemeProvider _tumblrMemeProvider;
    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<CultBotDbContext> _contextFactory;

    public MemePostingService(
        DiscordSocketClient client,
        TumblrMemeProvider tumblrMemeProvider,
        HttpClient httpClient,
        IDbContextFactory<CultBotDbContext> contextFactory)
    {
        _client = client;
        _tumblrMemeProvider = tumblrMemeProvider;
        _httpClient = httpClient;
        _contextFactory = contextFactory;
    }

    public bool IsEnabled(out string reason)
    {
        if (BotConfig.MemesChannelId == 0)
        {
            reason = "MemesChannelId is 0";
            return false;
        }

        if (!_tumblrMemeProvider.IsConfigured)
        {
            reason = "TUMBLR_CONSUMER_KEY is missing";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public Task<MemePostResult> PostManualMemeAsync(ulong? targetGuildId = null, CancellationToken cancellationToken = default)
    {
        return PostMemeAsync(MemePostRequest.Admin(targetGuildId), cancellationToken);
    }

    public Task<MemePostResult> PostUserRequestedMemeAsync(
        ulong userId,
        ulong targetGuildId,
        CancellationToken cancellationToken = default)
    {
        return PostMemeAsync(MemePostRequest.User(userId, targetGuildId), cancellationToken);
    }

    public Task<MemePostResult> PostScheduledMemeAsync(MemeSlot slot, CancellationToken cancellationToken = default)
    {
        return PostMemeAsync(MemePostRequest.Scheduled(slot), cancellationToken);
    }

    private async Task<MemePostResult> PostMemeAsync(MemePostRequest request, CancellationToken cancellationToken)
    {
        await _postLock.WaitAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            if (request.Kind == MemePostRequestKind.User)
            {
                var limitResult = await CheckUserRequestLimitsAsync(context, request, nowUtc, cancellationToken);
                if (limitResult != null)
                    return limitResult;
            }

            var result = await PostMemeCoreAsync(context, request, cancellationToken);

            if (request.Kind == MemePostRequestKind.User)
            {
                await RecordUserRequestAsync(context, request, result, nowUtc, cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR posting meme: {ex.Message}");
            var result = new MemePostResult(
                MemePostStatus.Failed,
                false,
                false,
                "I couldn't fetch a meme right now. Try again in a bit.");

            if (request.Kind == MemePostRequestKind.User)
            {
                await TryRecordFailedUserRequestAsync(request, result, nowUtc, cancellationToken);
            }

            return result;
        }
        finally
        {
            _postLock.Release();
        }
    }

    private async Task<MemePostResult?> CheckUserRequestLimitsAsync(
        CultBotDbContext context,
        MemePostRequest request,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!request.RequestedByUserId.HasValue || !request.TargetGuildId.HasValue)
        {
            var result = new MemePostResult(
                MemePostStatus.Unauthorized,
                false,
                true,
                "Use this command inside the server.");
            await RecordUserRequestAsync(context, request, result, nowUtc, cancellationToken);
            return result;
        }

        var userId = request.RequestedByUserId.Value;
        var dateKey = MemeSchedule.GetEasternDateKey(nowUtc);
        var successesToday = await context.MemeRequestUsages
            .CountAsync(
                u => u.UserId == userId &&
                    u.EasternDateKey == dateKey &&
                    u.Result == nameof(MemePostStatus.Posted),
                cancellationToken);

        if (successesToday >= BotConfig.MemeDailyUserRequestLimit)
        {
            var result = new MemePostResult(
                MemePostStatus.DailyLimit,
                false,
                true,
                $"You've used your {BotConfig.MemeDailyUserRequestLimit} meme requests for today. Try again tomorrow.");
            await RecordUserRequestAsync(context, request, result, nowUtc, cancellationToken);
            return result;
        }

        var lastAttempt = await context.MemeRequestUsages
            .Where(u => u.UserId == userId &&
                u.Result != nameof(MemePostStatus.Cooldown) &&
                u.Result != nameof(MemePostStatus.DailyLimit) &&
                u.Result != nameof(MemePostStatus.Unauthorized))
            .OrderByDescending(u => u.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var cooldown = TimeSpan.FromMinutes(BotConfig.MemeUserRequestCooldownMinutes);
        if (lastAttempt != null)
        {
            var retryAfter = cooldown - (nowUtc - lastAttempt.RequestedAtUtc);
            if (retryAfter > TimeSpan.Zero)
            {
                var result = new MemePostResult(
                    MemePostStatus.Cooldown,
                    false,
                    true,
                    $"Slow down just a touch. You can request another meme in {FormatDuration(retryAfter)}.",
                    retryAfter);
                await RecordUserRequestAsync(context, request, result, nowUtc, cancellationToken);
                return result;
            }
        }

        return null;
    }

    private async Task<MemePostResult> PostMemeCoreAsync(
        CultBotDbContext context,
        MemePostRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled(out var disabledReason))
        {
            return new MemePostResult(
                MemePostStatus.Disabled,
                false,
                true,
                $"I can't fetch memes yet because meme posting is not configured ({disabledReason}).");
        }

        var channels = GetTargetChannels(request);
        Console.WriteLine($"Meme post request {request.Kind}: target channels found = {channels.Count}.");
        if (channels.Count == 0)
        {
            return new MemePostResult(
                MemePostStatus.Disabled,
                false,
                true,
                "I can't find the configured memes channel.");
        }

        if (request.Kind == MemePostRequestKind.Scheduled)
        {
            var guildIds = channels.Select(x => x.Guild.Id).ToList();
            var alreadyPostedCount = await context.PostedMemes
                .CountAsync(
                    p => guildIds.Contains(p.GuildId) &&
                        p.ScheduledSlotUtc == request.Slot.ScheduledForUtc,
                    cancellationToken);

            if (alreadyPostedCount >= channels.Count)
            {
                return new MemePostResult(
                    MemePostStatus.AlreadyPosted,
                    false,
                    true,
                    $"Meme slot {request.Slot.LocalLabel} was already posted.");
            }
        }

        var postedSourceIds = await context.PostedMemes
            .Where(p => p.Source == "Tumblr")
            .Select(p => p.SourcePostId)
            .ToListAsync(cancellationToken);
        var postedHashes = await context.PostedMemes
            .Select(p => p.ImageSha256)
            .ToListAsync(cancellationToken);

        var excludedSourceIds = new HashSet<string>(postedSourceIds, StringComparer.OrdinalIgnoreCase);
        var excludedHashes = new HashSet<string>(postedHashes, StringComparer.OrdinalIgnoreCase);
        var fetchResult = await _tumblrMemeProvider.GetCandidatesAsync(excludedSourceIds, cancellationToken);
        Console.WriteLine($"Tumblr meme fetch result: {fetchResult.Status}, candidates = {fetchResult.Candidates.Count}.");

        if (fetchResult.Status == MemeFetchStatus.RateLimited)
        {
            return new MemePostResult(
                MemePostStatus.RateLimited,
                false,
                true,
                "The meme source is rate limiting me right now, so I can't fetch a meme. Try again later.");
        }

        if (fetchResult.Status != MemeFetchStatus.Success)
        {
            return new MemePostResult(
                MemePostStatus.SourceUnavailable,
                false,
                true,
                "I can't reach the meme source right now. Try again later.");
        }

        if (fetchResult.Candidates.Count == 0)
        {
            return new MemePostResult(
                MemePostStatus.NoMemeAvailable,
                false,
                true,
                "I couldn't find a fresh safe image meme right now. Try again later.");
        }

        var sawDownloadRateLimit = false;
        var sawDownloadFailure = false;

        foreach (var candidate in fetchResult.Candidates)
        {
            var downloaded = await DownloadImageAsync(candidate, cancellationToken);
            if (downloaded.Status == MemeDownloadStatus.RateLimited)
            {
                sawDownloadRateLimit = true;
                continue;
            }

            if (downloaded.Status == MemeDownloadStatus.Failed)
            {
                sawDownloadFailure = true;
                continue;
            }

            if (downloaded.Image == null ||
                excludedHashes.Contains(downloaded.Image.ImageSha256))
            {
                continue;
            }

            var sentCount = 0;
            foreach (var target in channels)
            {
                if (request.Kind == MemePostRequestKind.Scheduled)
                {
                    var alreadyPosted = await context.PostedMemes.AnyAsync(
                        p => p.GuildId == target.Guild.Id &&
                            p.ScheduledSlotUtc == request.Slot.ScheduledForUtc,
                        cancellationToken);
                    if (alreadyPosted)
                        continue;
                }

                using var stream = new MemoryStream(downloaded.Image.Bytes, writable: false);
                var message = await target.Channel.SendFileAsync(stream, downloaded.Image.FileName);

                context.PostedMemes.Add(new PostedMeme
                {
                    GuildId = target.Guild.Id,
                    ChannelId = target.Channel.Id,
                    DiscordMessageId = message.Id,
                    Source = candidate.Source,
                    SourcePostId = candidate.SourcePostId,
                    ImageUrl = candidate.ImageUrl,
                    ImageSha256 = downloaded.Image.ImageSha256,
                    ScheduledSlotUtc = request.Slot.ScheduledForUtc,
                    ScheduledSlotLocal = request.Slot.LocalLabel,
                    PostedAtUtc = DateTime.UtcNow
                });

                await context.SaveChangesAsync(cancellationToken);
                sentCount++;
                Console.WriteLine($"Posted meme {candidate.Source}:{candidate.SourcePostId} to #{target.Channel.Name} for slot {request.Slot.LocalLabel}.");
            }

            if (sentCount > 0)
            {
                return new MemePostResult(
                    MemePostStatus.Posted,
                    true,
                    false,
                    "Posted a meme in the memes channel.");
            }
        }

        if (sawDownloadRateLimit)
        {
            return new MemePostResult(
                MemePostStatus.RateLimited,
                false,
                true,
                "The image host is rate limiting me right now, so I can't fetch a meme. Try again later.");
        }

        if (sawDownloadFailure)
        {
            return new MemePostResult(
                MemePostStatus.SourceUnavailable,
                false,
                true,
                "I couldn't download a meme image right now. Try again later.");
        }

        return new MemePostResult(
            MemePostStatus.NoMemeAvailable,
            false,
            true,
            "I couldn't find a fresh safe image meme right now. Try again later.");
    }

    private List<MemeChannelTarget> GetTargetChannels(MemePostRequest request)
    {
        var guilds = request.TargetGuildId.HasValue
            ? _client.Guilds.Where(guild => guild.Id == request.TargetGuildId.Value)
            : _client.Guilds;

        return guilds
            .Select(guild => new MemeChannelTarget(guild, guild.GetTextChannel(BotConfig.MemesChannelId)))
            .Where(target => target.Channel != null)
            .Select(target => target!)
            .ToList();
    }

    private async Task RecordUserRequestAsync(
        CultBotDbContext context,
        MemePostRequest request,
        MemePostResult result,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken)
    {
        context.MemeRequestUsages.Add(new MemeRequestUsage
        {
            UserId = request.RequestedByUserId ?? 0,
            GuildId = request.TargetGuildId ?? 0,
            RequestedAtUtc = requestedAtUtc,
            EasternDateKey = MemeSchedule.GetEasternDateKey(requestedAtUtc),
            Result = result.Status.ToString(),
            Reason = Truncate(result.Message, 500)
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task TryRecordFailedUserRequestAsync(
        MemePostRequest request,
        MemePostResult result,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await RecordUserRequestAsync(context, request, result, requestedAtUtc, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR recording failed meme request: {ex.Message}");
        }
    }

    private async Task<MemeDownloadResult> DownloadImageAsync(
        MemeCandidate candidate,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, candidate.ImageUrl);
        request.Headers.UserAgent.TryParseAdd("ExpiredSodaCultBot/1.0");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return new MemeDownloadResult(MemeDownloadStatus.RateLimited, null);

        if (!response.IsSuccessStatusCode)
            return new MemeDownloadResult(MemeDownloadStatus.Failed, null);

        if (response.Content.Headers.ContentLength > BotConfig.MemeMaxImageBytes)
            return new MemeDownloadResult(MemeDownloadStatus.Rejected, null);

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) &&
            mediaType is not ("image/jpeg" or "image/png"))
        {
            return new MemeDownloadResult(MemeDownloadStatus.Rejected, null);
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;

        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            output.Write(buffer, 0, read);
            if (output.Length > BotConfig.MemeMaxImageBytes)
                return new MemeDownloadResult(MemeDownloadStatus.Rejected, null);
        }

        var bytes = output.ToArray();
        if (bytes.Length == 0)
            return new MemeDownloadResult(MemeDownloadStatus.Rejected, null);

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var image = new DownloadedMemeImage(
            bytes,
            hash,
            $"tumblr-meme-{candidate.SourcePostId}.{candidate.FileExtension}");
        return new MemeDownloadResult(MemeDownloadStatus.Success, image);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes));
        return minutes == 1 ? "1 minute" : $"{minutes} minutes";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record MemeChannelTarget(
        SocketGuild Guild,
        SocketTextChannel Channel);

    private sealed record MemeDownloadResult(
        MemeDownloadStatus Status,
        DownloadedMemeImage? Image);

    private enum MemeDownloadStatus
    {
        Success,
        Rejected,
        RateLimited,
        Failed
    }

    private sealed record DownloadedMemeImage(
        byte[] Bytes,
        string ImageSha256,
        string FileName);
}
