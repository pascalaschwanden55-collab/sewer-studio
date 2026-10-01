using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingOpenStretchDamagePolicy
{
    /// <summary>
    /// Offene Streckenschaeden-Anfaenge. Die beim Schliessen erzeugte Endmarke traegt
    /// selbst IsStreckenschaden=true und MeterEnd=null und galt hier frueher
    /// faelschlich als weiterer offener Schaden.
    /// </summary>
    public static IReadOnlyList<CodingEvent> FindOpen(IEnumerable<CodingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return CodingStretchDamageDisplayPolicy.FindOpenStarts(events);
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
