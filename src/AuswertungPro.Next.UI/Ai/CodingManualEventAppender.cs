using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingManualEventAppender
{
    public static CodingEvent Apply(
        CodingManualEventDraft draft,
        OverlayGeometry? overlay,
        ICodingSessionService codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(codingSessionService);

        var ev = codingSessionService.AddEvent(draft.Entry, overlay);
        ev.AiContext = draft.AiContext;
        return ev;
    }

    public static CodingEvent Apply(
        ProtocolEntry entry,
        OverlayGeometry? overlay,
        ICodingSessionService codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(codingSessionService);

        var ev = codingSessionService.AddEvent(entry, overlay);
        ev.AiContext = CodingManualEventFactory.CreateUnconfirmedContext(entry.Code ?? "");
        return ev;
    }
}
