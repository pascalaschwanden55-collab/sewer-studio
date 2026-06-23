namespace AuswertungPro.Next.UI.Player;

public static class PlayerClock
{
    public static DateTime Now(TimeProvider? timeProvider = null)
        => LocalNow(timeProvider).DateTime;

    public static DateTime UtcNow(TimeProvider? timeProvider = null)
        => (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public static DateTimeOffset NowOffset(TimeProvider? timeProvider = null)
        => LocalNow(timeProvider);

    private static DateTimeOffset LocalNow(TimeProvider? timeProvider)
    {
        var provider = timeProvider ?? TimeProvider.System;
        return TimeZoneInfo.ConvertTime(provider.GetUtcNow(), provider.LocalTimeZone);
    }
}
