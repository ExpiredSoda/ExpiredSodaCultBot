using CultBot.Configuration;

namespace CultBot.Features.Memes;

public sealed record MemeSlot(
    DateTime ScheduledForUtc,
    string LocalLabel,
    bool IsManual);

public static class MemeSchedule
{
    public static MemeSlot CreateManualSlot(DateTime nowUtc)
    {
        var eastern = GetEasternTimeZone();
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, eastern);
        return new MemeSlot(nowUtc, $"manual-{local:yyyy-MM-dd-HHmmss}", true);
    }

    public static IReadOnlyList<MemeSlot> GetDueSlots(DateTime nowUtc)
    {
        var eastern = GetEasternTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, eastern);
        var gracePeriod = TimeSpan.FromMinutes(BotConfig.MemeRetryGraceMinutes);
        var slots = new List<MemeSlot>();

        foreach (var time in BotConfig.MemePostTimesEastern)
        {
            var localSlot = new DateTime(
                nowLocal.Year,
                nowLocal.Month,
                nowLocal.Day,
                time.Hour,
                time.Minute,
                0,
                DateTimeKind.Unspecified);
            var slotUtc = TimeZoneInfo.ConvertTimeToUtc(localSlot, eastern);

            if (nowUtc >= slotUtc && nowUtc <= slotUtc.Add(gracePeriod))
            {
                slots.Add(new MemeSlot(slotUtc, $"{localSlot:yyyy-MM-dd-HHmm}", false));
            }
        }

        return slots;
    }

    public static string GetEasternDateKey(DateTime utc)
    {
        var eastern = GetEasternTimeZone();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, eastern);
        return local.ToString("yyyy-MM-dd");
    }

    private static TimeZoneInfo GetEasternTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }
}
