using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingManualEventDraft(
    ProtocolEntry Entry,
    CodingEventReviewContext ReviewContext);

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

        return new CodingManualEventDraft(entry, CreateUnconfirmedContext());
    }

    public static CodingEventReviewContext CreateUnconfirmedContext()
        => new()
        {
            Reason = "Manuell codiert - bitte bestätigen",
            Decision = CodingUserDecision.Ignored
        };
}
