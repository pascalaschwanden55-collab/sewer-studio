using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingStretchDamageManualCloseResultKind
{
    RequiresLaterMeter,
    Closed
}

public sealed record CodingStretchDamageManualCloseResult(
    CodingStretchDamageManualCloseResultKind Kind,
    CodingEvent? EndEvent,
    string? StatusText);

public static class CodingStretchDamageManualCloseApplier
{
    public static CodingStretchDamageManualCloseResult Apply(
        CodingEvent startEvent,
        double currentMeter,
        TimeSpan currentVideoTime,
        ICodingSessionService codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(startEvent);
        ArgumentNullException.ThrowIfNull(codingSessionService);

        if (!CodingStretchDamageClosePolicy.CanClose(startEvent.MeterAtCapture, currentMeter))
            return new CodingStretchDamageManualCloseResult(
                CodingStretchDamageManualCloseResultKind.RequiresLaterMeter,
                EndEvent: null,
                StatusText: null);

        var endEntry = CodingStreckenschadenEventFactory.CloseStart(startEvent.Entry, currentMeter);
        var endEvent = codingSessionService.AddEvent(endEntry, null);
        endEvent.VideoTimestamp = currentVideoTime;

        var statusText = CodingStretchDamageClosePolicy.BuildClosedStatusText(
            startEvent.Entry.Code,
            startEvent.MeterAtCapture,
            currentMeter);

        return new CodingStretchDamageManualCloseResult(
            CodingStretchDamageManualCloseResultKind.Closed,
            endEvent,
            statusText);
    }
}
