using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

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
            DefectStatus.AutoAccepted => "KI-Kriterien erfüllt",
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
            DefectStatus.AutoAccepted => "\uE73E",
            DefectStatus.Accepted => "\uE73E",
            DefectStatus.AcceptedWithEdit => "\uE70F",
            DefectStatus.Pending => "\uE823",
            DefectStatus.ReviewRequired => "\uE7BA",
            DefectStatus.Rejected => "\uE711",
            _ => ""
        };

    public static CodingInlineDefectDetailState BuildInlineDetail(CodingEvent ev)
    {
        var status = DefectStatusPolicy.GetStatus(ev);
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
            CanAct: DefectStatusPolicy.CanAct(ev));
    }
}
