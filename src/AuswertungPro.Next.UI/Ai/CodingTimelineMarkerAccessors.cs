using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

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
           && CodingSessionViewModel.GetDefectStatus(ev) == DefectStatus.Rejected;
}
