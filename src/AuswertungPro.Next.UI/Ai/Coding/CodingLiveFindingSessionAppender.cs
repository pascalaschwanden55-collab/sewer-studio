using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingLiveFindingSessionAppender
{
    public static CodingEvent Append(
        CodingLiveFindingEventDraft draft,
        Action<ProtocolEntry> attachAnalyzedFramePhoto,
        ICodingSessionService codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(codingSessionService);

        return Append(
            draft,
            attachAnalyzedFramePhoto,
            entry => codingSessionService.AddEvent(entry));
    }

    public static CodingEvent Append(
        CodingLiveFindingEventDraft draft,
        Action<ProtocolEntry> attachAnalyzedFramePhoto,
        Func<ProtocolEntry, CodingEvent> addEvent)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(attachAnalyzedFramePhoto);
        ArgumentNullException.ThrowIfNull(addEvent);

        attachAnalyzedFramePhoto(draft.Entry);
        var codingEvent = addEvent(draft.Entry);
        codingEvent.AiContext = draft.AiContext;
        codingEvent.Overlay = draft.Overlay;
        return codingEvent;
    }
}
