using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingOpenStretchDamageCloseApplier
{
    public static bool Apply(
        IReadOnlyList<CodingEvent> openEvents,
        double currentMeter,
        ICodingSessionService? codingSessionService)
    {
        if (openEvents.Count == 0)
            return false;

        foreach (var ev in openEvents)
        {
            ev.Entry.MeterEnd = CodingOpenStretchDamagePolicy.ResolveCloseMeter(ev, currentMeter);
            codingSessionService?.UpdateEvent(ev.EventId, ev.Entry, ev.Overlay);
        }

        return true;
    }
}
