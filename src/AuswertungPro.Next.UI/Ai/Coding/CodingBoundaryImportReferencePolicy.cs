using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingBoundaryReference(double Meter, TimeSpan VideoTime);

public static class CodingBoundaryImportReferencePolicy
{
    public static CodingBoundaryReference ResolveStart(IEnumerable<CodingEvent> importEvents)
    {
        var importBcd = FindImportCode(importEvents, "BCD");
        return importBcd == null
            ? new CodingBoundaryReference(0.0, TimeSpan.Zero)
            : new CodingBoundaryReference(importBcd.MeterAtCapture, importBcd.VideoTimestamp);
    }

    public static CodingBoundaryReference ResolveEnd(
        IEnumerable<CodingEvent> importEvents,
        double? osdMeter,
        double fallbackEndMeter,
        double vmEndMeter,
        TimeSpan fallbackVideoTime)
    {
        var importBce = FindImportCode(importEvents, "BCE");
        var meter = CodingDedupPolicy.ResolvePlausibleEndMeter(
            osdMeter ?? fallbackEndMeter,
            importBce?.MeterAtCapture,
            vmEndMeter);

        var videoTime = importBce != null
                        && Math.Abs(importBce.MeterAtCapture - meter) < 0.01
            ? importBce.VideoTimestamp
            : fallbackVideoTime;

        return new CodingBoundaryReference(meter, videoTime);
    }

    private static CodingEvent? FindImportCode(IEnumerable<CodingEvent> importEvents, string code)
    {
        foreach (var importEvent in importEvents)
        {
            if (string.Equals(importEvent.Entry.Code, code, StringComparison.OrdinalIgnoreCase))
                return importEvent;
        }

        return null;
    }
}
