using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingEventEditApplier
{
    public static void Apply(
        CodingEvent codingEvent,
        ICodingSessionService? codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);

        var entry = codingEvent.Entry;
        codingEvent.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? codingEvent.MeterAtCapture;
        codingEvent.VideoTimestamp = entry.Zeit ?? codingEvent.VideoTimestamp;
        codingSessionService?.UpdateEvent(codingEvent.EventId, entry, codingEvent.Overlay);
    }
}
