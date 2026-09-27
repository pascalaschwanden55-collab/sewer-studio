using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingMultiModelEventAppender
{
    public static CodingEvent Apply(
        CodingMultiModelEventDraft draft,
        ICodingSessionService codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(codingSessionService);

        var expected = CodingPointFollowUpPolicy.Fingerprint(draft.Entry, draft.Overlay, draft.AiContext,
            draft.Entry.MeterStart ?? codingSessionService.CurrentMeter, draft.Entry.Zeit ?? TimeSpan.Zero);
        var ev = codingSessionService.AddEvent(draft.Entry);
        // EventAdded kann synchron eine Bearbeitung ausloesen. Deren Kontext/Geometrie
        // darf weder ueberschrieben noch nachtraeglich als unberuehrt registriert werden.
        if (ev.AiContext is not null || ev.Overlay is not null)
        {
            ev.AiContext ??= draft.AiContext;
            CodingPointFollowUpPolicy.MarkHumanTouched(ev);
            return ev;
        }
        ev.AiContext = draft.AiContext;
        ev.Overlay = draft.Overlay;
        CodingPointFollowUpPolicy.RegisterNew(codingSessionService.ActiveSession, ev, expected);
        return ev;
    }
}
