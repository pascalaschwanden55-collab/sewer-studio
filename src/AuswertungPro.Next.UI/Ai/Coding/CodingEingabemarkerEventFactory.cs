using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingEingabemarkerEventDraft(
    ProtocolEntry Entry,
    CodingEventAiContext AiContext);

public static class CodingEingabemarkerEventFactory
{
    public static CodingEingabemarkerEventDraft CreateAccepted(
        string code,
        string description,
        string keyword,
        double meter,
        TimeSpan videoTime)
    {
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = code,
            Beschreibung = description,
            MeterStart = meter,
            Zeit = videoTime
        };

        var aiContext = new CodingEventAiContext
        {
            SuggestedCode = code,
            Confidence = 1.0,
            Reason = $"Eingabemarker: {keyword}",
            Decision = CodingUserDecision.Accepted
        };

        return new CodingEingabemarkerEventDraft(entry, aiContext);
    }
}
