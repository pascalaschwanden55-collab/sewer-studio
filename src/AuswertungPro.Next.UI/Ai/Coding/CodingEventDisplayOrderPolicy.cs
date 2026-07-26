using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingEventDisplayOrderPolicy
{
    public static IReadOnlyList<CodingEvent> Order(IEnumerable<CodingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return events
            .OrderBy(e => e.MeterAtCapture)
            .ThenBy(e => e.VideoTimestamp)
            .ToList();
    }
}
