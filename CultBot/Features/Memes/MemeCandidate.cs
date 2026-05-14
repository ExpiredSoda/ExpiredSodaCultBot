namespace CultBot.Features.Memes;

public sealed record MemeCandidate(
    string Source,
    string SourcePostId,
    string ImageUrl,
    string FileExtension,
    string Permalink);

public enum MemeFetchStatus
{
    Success,
    NotConfigured,
    RateLimited,
    Failed
}

public sealed record MemeFetchResult(
    MemeFetchStatus Status,
    IReadOnlyList<MemeCandidate> Candidates,
    string? Message = null)
{
    public static MemeFetchResult Success(IReadOnlyList<MemeCandidate> candidates) =>
        new(MemeFetchStatus.Success, candidates);
}
