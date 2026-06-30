using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingTimelineMarkerAccessors
{
    public static double Meter(object marker)
        => marker is CodingEvent ev ? ev.MeterAtCapture : 0;

    public static string Code(object marker)
        => marker is CodingEvent ev ? ev.Entry.Code : "?";

    public static double Confidence(object marker)
        => marker is CodingEvent { AiContext: not null } ev ? ev.AiContext.Confidence : -1;

    public static bool IsRejected(object marker)
        => marker is CodingEvent ev
           && DefectStatusPolicy.GetStatus(ev) == DefectStatus.Rejected;
}
