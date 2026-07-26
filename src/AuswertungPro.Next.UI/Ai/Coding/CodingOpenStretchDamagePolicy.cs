using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingOpenStretchDamagePolicy
{
    public static IReadOnlyList<CodingEvent> FindOpen(IEnumerable<CodingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return events
            .Where(e => e.Entry.IsStreckenschaden && !e.Entry.MeterEnd.HasValue)
            .ToList();
    }

    public static double ResolveCloseMeter(CodingEvent codingEvent, double currentMeter)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);

        var start = codingEvent.Entry.MeterStart ?? 0;
        return codingEvent.MeterAtCapture > start
            ? codingEvent.MeterAtCapture
            : currentMeter;
    }
}
