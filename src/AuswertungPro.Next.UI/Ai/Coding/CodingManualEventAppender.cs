using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

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
        ev.ReviewContext = draft.ReviewContext;
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
        ev.ReviewContext = CodingManualEventFactory.CreateUnconfirmedContext();
        return ev;
    }
}
