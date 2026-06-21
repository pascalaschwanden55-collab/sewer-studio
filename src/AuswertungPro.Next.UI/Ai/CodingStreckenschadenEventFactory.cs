using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingStreckenschadenEventDraft(
    ProtocolEntry Entry,
    CodingEventAiContext AiContext);

public static class CodingStreckenschadenEventFactory
{
    public static CodingStreckenschadenEventDraft CreateOpen(
        string code,
        string? label,
        double startMeter,
        TimeSpan videoTime)
    {
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = code,
            Beschreibung = label ?? code,
            MeterStart = startMeter,
            MeterEnd = null,
            IsStreckenschaden = true,
            Zeit = videoTime
        };

        var aiContext = new CodingEventAiContext
        {
            SuggestedCode = code,
            Confidence = 0.0,
            Reason = "Streckenschaden-Anfang (automatisch) - noch offen",
            Decision = CodingUserDecision.Ignored
        };

        return new CodingStreckenschadenEventDraft(entry, aiContext);
    }
}
