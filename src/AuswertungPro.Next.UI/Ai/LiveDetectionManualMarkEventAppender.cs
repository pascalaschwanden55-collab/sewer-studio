using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class LiveDetectionManualMarkEventAppender
{
    public static CodingEvent Apply(
        ProtocolEntry selectedEntry,
        double fallbackMeter,
        TimeSpan fallbackTime,
        OverlayGeometry overlay,
        ICodingSessionService codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(selectedEntry);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(codingSessionService);

        var manualEntry = CodingExplorerEntryFactory.CreateManualFromSelected(
            selectedEntry,
            fallbackMeter,
            fallbackTime);

        return codingSessionService.AddEvent(manualEntry, overlay);
    }
}
