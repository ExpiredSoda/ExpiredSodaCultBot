namespace CultBot.Features.Memes;

public enum MemePostStatus
{
    Posted,
    Cooldown,
    DailyLimit,
    Disabled,
    AlreadyPosted,
    NoMemeAvailable,
    SourceUnavailable,
    RateLimited,
    Unauthorized,
    Failed
}

public sealed record MemePostResult(
    MemePostStatus Status,
    bool Success,
    bool Skipped,
    string Message,
    TimeSpan? RetryAfter = null,
    string? Source = null,
    string? SourcePostId = null,
    string? ImageUrl = null,
    string? SourcePermalink = null,
    string? ImageSha256 = null,
    ulong? DiscordMessageId = null,
    ulong? ChannelId = null);
