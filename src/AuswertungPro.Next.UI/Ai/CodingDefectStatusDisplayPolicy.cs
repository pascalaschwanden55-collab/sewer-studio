using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingInlineDefectDetailState(
    string CodeText,
    string DescriptionText,
    string DistanceText,
    string ConfidenceText,
    double? Confidence,
    DefectStatus Status,
    string StatusText,
    bool CanAct);

public static class CodingDefectStatusDisplayPolicy
{
    public static string DisplayText(DefectStatus status)
        => status switch
        {
            DefectStatus.AutoAccepted => "Auto-Akzeptiert (Green Zone)",
            DefectStatus.Pending => "Review empfohlen (Yellow Zone)",
            DefectStatus.ReviewRequired => "Manuell erforderlich (Red Zone)",
            DefectStatus.Accepted => "Akzeptiert",
            DefectStatus.AcceptedWithEdit => "Bearbeitet",
            DefectStatus.Rejected => "Abgelehnt",
            _ => ""
        };

    public static Color ZoneDotColor(DefectStatus status)
        => status switch
        {
            DefectStatus.Accepted or DefectStatus.AutoAccepted => Color.FromRgb(0x22, 0xC5, 0x5E),
            DefectStatus.AcceptedWithEdit => Color.FromRgb(0x3B, 0x82, 0xF6),
            DefectStatus.Rejected => Color.FromRgb(0xEF, 0x44, 0x44),
            _ => Color.FromRgb(0x94, 0xA3, 0xB8)
        };

    public static string StatusIcon(DefectStatus status)
        => status switch
        {
            DefectStatus.AutoAccepted => "\u2713",
            DefectStatus.Accepted => "\u2713",
            DefectStatus.AcceptedWithEdit => "\u270E",
            DefectStatus.Pending => "\u23F3",
            DefectStatus.ReviewRequired => "\u26A0",
            DefectStatus.Rejected => "\u2717",
            _ => ""
        };

    public static CodingInlineDefectDetailState BuildInlineDetail(CodingEvent ev)
    {
        var status = CodingSessionViewModel.GetDefectStatus(ev);
        var confidence = ev.AiContext?.Confidence;

        return new CodingInlineDefectDetailState(
            CodeText: ev.Entry.Code,
            DescriptionText: ev.Entry.Beschreibung,
            DistanceText: $"{ev.MeterAtCapture:F2}m",
            ConfidenceText: confidence.HasValue
                ? $"{confidence.Value * 100:F0}%"
                : "\u2013",
            Confidence: confidence,
            Status: status,
            StatusText: DisplayText(status),
            CanAct: CodingSessionViewModel.CanActOnDefect(ev));
    }
}
