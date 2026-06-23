using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingMultiModelEventAppender
{
    public static CodingEvent Apply(
        CodingMultiModelEventDraft draft,
        ICodingSessionService codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(codingSessionService);

        var ev = codingSessionService.AddEvent(draft.Entry);
        ev.AiContext = draft.AiContext;
        ev.Overlay = draft.Overlay;
        return ev;
    }
}
