using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingEventSeekPolicy
{
    public static bool TryGetSeekMilliseconds(CodingEvent codingEvent, out long milliseconds)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);

        milliseconds = 0;
        if (codingEvent.VideoTimestamp.TotalMilliseconds < 0)
            return false;

        if (!codingEvent.Entry.Zeit.HasValue && codingEvent.VideoTimestamp == TimeSpan.Zero)
            return false;

        milliseconds = (long)codingEvent.VideoTimestamp.TotalMilliseconds;
        return true;
    }
}
