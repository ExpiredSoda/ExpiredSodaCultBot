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
    ulong? RequestedByUserId = null,
    ulong? TargetGuildId = null)
{
    public static MemePostRequest Scheduled(MemeSlot slot) =>
        new(MemePostRequestKind.Scheduled, slot);

    public static MemePostRequest Admin(ulong? targetGuildId) =>
        new(MemePostRequestKind.Admin, MemeSchedule.CreateManualSlot(DateTime.UtcNow), TargetGuildId: targetGuildId);

    public static MemePostRequest User(ulong userId, ulong targetGuildId) =>
        new(MemePostRequestKind.User, MemeSchedule.CreateManualSlot(DateTime.UtcNow), userId, targetGuildId);
}
