using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingManualEventDraft(
    ProtocolEntry Entry,
    CodingEventAiContext AiContext);

public static class CodingManualEventFactory
{
    public static CodingManualEventDraft CreateUnconfirmed(
        string code,
        string? description,
        double meter,
        TimeSpan videoTime,
        OverlayGeometry? overlay)
    {
        var entry = new ProtocolEntry
        {
            Code = code,
            Beschreibung = description ?? code,
            MeterStart = meter,
            Zeit = videoTime,
            Source = ProtocolEntrySource.Manual
        };

        CodingOverlayQuantificationWriter.ApplyToEntry(entry, overlay);

        return new CodingManualEventDraft(entry, CreateUnconfirmedContext(code));
    }

    public static CodingEventAiContext CreateUnconfirmedContext(string code)
        => new()
        {
            SuggestedCode = code,
            Confidence = 1.0,
            Reason = "Manuell codiert - bitte bestätigen",
            Decision = CodingUserDecision.Ignored
        };
}
