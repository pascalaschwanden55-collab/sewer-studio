using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai;

public static class LiveDetectionDisplayPolicy
{
    public static Color QuickScanSeverityColor(int severity, bool hasDamage)
    {
        if (!hasDamage)
            return Color.FromArgb(100, 0x94, 0xA3, 0xB8);

        return severity switch
        {
            >= 4 => Color.FromRgb(0xEF, 0x44, 0x44),
            3 => Color.FromRgb(0xF5, 0x9E, 0x0B),
            2 => Color.FromRgb(0xFA, 0xCC, 0x15),
            _ => Color.FromRgb(0x22, 0xC5, 0x5E),
        };
    }

    public static string BuildQuickScanTooltip(QuickScanSegment segment)
    {
        if (!segment.HasDamage)
            return $"Kein Schaden @ {segment.TimestampSeconds:0.0}s";

        var tooltip = $"Schaden: {segment.Label ?? "?"} (Schwere {segment.Severity})";
        if (segment.Clock != null)
            tooltip += $"\nUhr: {segment.Clock}";
        return tooltip + $"\n@ {segment.TimestampSeconds:0.0}s";
    }

    public static string CompactModelName(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return "?";

        var trimmed = model.Trim();
        var slashIndex = trimmed.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < trimmed.Length - 1)
            trimmed = trimmed[(slashIndex + 1)..];
        return trimmed;
    }

    public static string BuildDetectionLabel(LiveFrameFinding finding)
    {
        var baseText = string.IsNullOrWhiteSpace(finding.VsaCodeHint)
            ? finding.Label
            : $"{finding.VsaCodeHint} {finding.Label}";
        if (baseText.Length > 24)
            baseText = baseText[..24] + "...";

        var clock = string.IsNullOrWhiteSpace(finding.PositionClock) ? "?" : finding.PositionClock;
        var extent = finding.ExtentPercent is > 0 ? $"{finding.ExtentPercent}%" : "";
        var extra = "";
        if (finding.HeightMm is > 0)
            extra += $" H:{finding.HeightMm}mm";
        if (finding.IntrusionPercent is > 0)
            extra += $" Einr:{finding.IntrusionPercent}%";
        return $"{clock}{(extent.Length > 0 ? $" / {extent}" : "")}{extra} - {baseText}";
    }

    public static Color DetectionSeverityColor(int severity)
        => Math.Clamp(severity, 1, 5) switch
        {
            >= 5 => Color.FromRgb(239, 68, 68),
            4 => Color.FromRgb(249, 115, 22),
            3 => Color.FromRgb(245, 158, 11),
            2 => Color.FromRgb(132, 204, 22),
            _ => Color.FromRgb(34, 197, 94)
        };
}
