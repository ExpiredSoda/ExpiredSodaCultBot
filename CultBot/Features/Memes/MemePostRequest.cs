namespace CultBot.Features.Memes;

public enum MemePostRequestKind
{
    Scheduled,
    Admin,
    User
}

public sealed record MemePostRequest(
    MemePostRequestKind Kind,
    MemeSlot Slot,
    string CommandName,
    ulong? RequestedByUserId = null,
    ulong? TargetGuildId = null,
    string? RequestedByUsername = null)
{
    public static MemePostRequest Scheduled(MemeSlot slot) =>
        new(MemePostRequestKind.Scheduled, slot, "scheduler");

    public static MemePostRequest Admin(ulong userId, ulong? targetGuildId, string? username) =>
        new(MemePostRequestKind.Admin, MemeSchedule.CreateManualSlot(DateTime.UtcNow), "/meme-now", userId, targetGuildId, username);

    public static MemePostRequest User(ulong userId, ulong targetGuildId, string? username) =>
        new(MemePostRequestKind.User, MemeSchedule.CreateManualSlot(DateTime.UtcNow), "/meme", userId, targetGuildId, username);
}
