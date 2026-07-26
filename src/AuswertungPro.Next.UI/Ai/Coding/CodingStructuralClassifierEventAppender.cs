using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingStructuralClassifierEventAppender
{
    public static CodingEvent Apply(
        CodingStructuralClassifierEventDraft draft,
        double meter,
        TimeSpan videoTime,
        ICodingSessionService codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(codingSessionService);

        var ev = codingSessionService.AddEvent(draft.Entry);
        ev.MeterAtCapture = meter;
        ev.VideoTimestamp = videoTime;
        ev.AiContext = draft.AiContext;
        return ev;
    }
}
