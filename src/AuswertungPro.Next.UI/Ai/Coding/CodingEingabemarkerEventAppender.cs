using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingEingabemarkerEventAppender
{
    public static CodingEvent Apply(
        CodingEingabemarkerEventDraft draft,
        OverlayGeometry? overlay,
        ICodingSessionService codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(codingSessionService);

        var ev = codingSessionService.AddEvent(draft.Entry, overlay);
        ev.AiContext = draft.AiContext;
        return ev;
    }
}
