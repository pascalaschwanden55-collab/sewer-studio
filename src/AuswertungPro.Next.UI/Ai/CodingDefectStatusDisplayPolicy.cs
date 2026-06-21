using System.Windows.Media;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

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
}
