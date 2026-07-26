using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingBoundaryEventDraft(
    ProtocolEntry Entry,
    CodingEventAiContext AiContext);

public static class CodingBoundaryEventFactory
{
    public static CodingBoundaryEventDraft CreateStart(string label, double meter, TimeSpan videoTime)
        => Create("BCD", label, meter, videoTime, "Rohranfang (Vorschlag - bitte bestätigen)");

    public static CodingBoundaryEventDraft CreateEnd(string label, double meter, TimeSpan videoTime)
        => Create("BCE", label, meter, videoTime, "Rohrende (Vorschlag - bitte bestätigen)");

    private static CodingBoundaryEventDraft Create(
        string code,
        string label,
        double meter,
        TimeSpan videoTime,
        string reason)
    {
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = code,
            Beschreibung = label,
            MeterStart = meter,
            Zeit = videoTime
        };

        var aiContext = new CodingEventAiContext
        {
            SuggestedCode = code,
            Confidence = 1.0,
            Reason = reason,
            Decision = CodingUserDecision.Ignored
        };

        return new CodingBoundaryEventDraft(entry, aiContext);
    }
}
