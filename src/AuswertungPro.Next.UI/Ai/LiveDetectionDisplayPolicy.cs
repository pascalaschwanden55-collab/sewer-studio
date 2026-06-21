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

    public static string BuildDetectionStatusText(LiveDetection result)
    {
        if (result.Error is not null)
            return $"Fehler: {result.Error}";

        var count = result.Findings.Count;
        return count > 0
            ? $"{count} Schaden erkannt @ {result.TimestampSeconds:0.0}s"
            : $"Kein Schaden @ {result.TimestampSeconds:0.0}s";
    }

    public static string BuildFindingSummaryText(IReadOnlyList<LiveFrameFinding> findings, int maxFindings = 3)
        => string.Join(" | ", findings.Take(maxFindings).Select(f =>
            $"{f.VsaCodeHint ?? f.Label} (S{f.Severity})"));

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

    public static string BuildFindingAssignmentTooltip(LiveFrameFinding finding)
        => $"Klick: Schadenscode zuweisen\n{finding.Label}"
            + (finding.VsaCodeHint != null ? $"\nVorschlag: {finding.VsaCodeHint}" : "")
            + $"\nSchwere: {finding.Severity}/5";

    public static string BuildDetectionConfirmationTitle(IReadOnlyList<LiveFrameFinding> findings)
    {
        if (findings.Count == 0)
            return string.Empty;

        var primary = findings[0];
        var severityText = FormatDetectionSeverity(primary.Severity);
        return findings.Count == 1
            ? $"KI-Erkennung: {primary.Label} ({severityText})"
            : $"KI-Erkennung: {findings.Count} Befunde - {primary.Label} ({severityText})";
    }

    public static string BuildDetectionConfirmationDetails(IReadOnlyList<LiveFrameFinding> findings)
        => string.Join("  |  ", findings.Select(f =>
        {
            var text = $"{f.PositionClock ?? "?"} Uhr - {f.Label}";
            if (f.ExtentPercent.HasValue)
                text += $" - {f.ExtentPercent}%";
            return text;
        }));

    public static Color DetectionSeverityColor(int severity)
        => Math.Clamp(severity, 1, 5) switch
        {
            >= 5 => Color.FromRgb(239, 68, 68),
            4 => Color.FromRgb(249, 115, 22),
            3 => Color.FromRgb(245, 158, 11),
            2 => Color.FromRgb(132, 204, 22),
            _ => Color.FromRgb(34, 197, 94)
        };

    private static string FormatDetectionSeverity(int severity)
        => severity switch
        {
            5 => "S5 kritisch",
            4 => "S4 schwer",
            3 => "S3 mittel",
            2 => "S2 leicht",
            _ => $"S{severity}"
        };
}
