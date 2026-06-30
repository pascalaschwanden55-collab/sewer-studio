using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Baut Anzeigetexte aus Live-Frame-Befunden fuer die Pipeline-Fortschrittsanzeige.
/// Reine, testbare Logik ohne UI-Abhaengigkeit.
/// </summary>
public static class LiveFindingSummaryBuilder
{
    /// <summary>
    /// Baut den Live-Frame-Info-Text aus Frames und Meterstand.
    /// Beispiel: "Frame 42/200  |  Meter 12.5 m"
    /// </summary>
    public static string BuildFrameInfo(int framesDone, int totalFrames, string? currentMeter)
    {
        var meterText = string.IsNullOrWhiteSpace(currentMeter) ? "—" : currentMeter;
        return $"Frame {framesDone}/{Math.Max(totalFrames, 0)}  |  Meter {meterText}";
    }

    /// <summary>
    /// Baut die Quantifizierungs-Zusammenfassung aus einer Liste von Live-Befunden.
    /// Gibt maximal die ersten 4 Befunde in kompakter Form aus.
    /// </summary>
    public static string BuildQuantSummary(IReadOnlyList<LiveFrameFinding> findings)
    {
        if (findings.Count == 0)
            return "Quantifizierung: keine Punkte erkannt";

        var parts = findings.Take(4).Select(f =>
        {
            var clock = string.IsNullOrWhiteSpace(f.PositionClock) ? "?" : f.PositionClock;
            var quantParts = new List<string>();
            if (f.ExtentPercent is > 0) quantParts.Add($"{f.ExtentPercent}%");
            if (f.HeightMm is > 0) quantParts.Add($"H:{f.HeightMm}mm");
            if (f.WidthMm is > 0) quantParts.Add($"B:{f.WidthMm}mm");
            if (f.IntrusionPercent is > 0) quantParts.Add($"Einr:{f.IntrusionPercent}%");
            if (f.CrossSectionReductionPercent is > 0) quantParts.Add($"QV:{f.CrossSectionReductionPercent}%");
            if (f.DiameterReductionMm is > 0) quantParts.Add($"DV:{f.DiameterReductionMm}mm");
            var quantStr = quantParts.Count > 0 ? string.Join(" ", quantParts) : "n/a";
            return $"{clock} ({quantStr})";
        });

        return "Q: " + string.Join(" | ", parts);
    }

    /// <summary>
    /// Baut das kompakte Label eines einzelnen Live-Befunds fuer die Overlay-Anzeige.
    /// Format: "Uhrlage / Ausdehnung [Quant] - Bezeichnung"
    /// </summary>
    public static string BuildFindingLabel(LiveFrameFinding finding)
    {
        var baseText = string.IsNullOrWhiteSpace(finding.VsaCodeHint)
            ? finding.Label
            : $"{finding.VsaCodeHint} {finding.Label}";
        if (baseText.Length > 20)
            baseText = baseText[..20] + "...";

        var clock = string.IsNullOrWhiteSpace(finding.PositionClock) ? "?" : finding.PositionClock;
        var extent = finding.ExtentPercent is > 0 ? $"{finding.ExtentPercent}%" : "n/a";
        var quantExtra = "";
        if (finding.HeightMm is > 0) quantExtra += $" H:{finding.HeightMm}mm";
        if (finding.IntrusionPercent is > 0) quantExtra += $" Einr:{finding.IntrusionPercent}%";
        if (finding.CrossSectionReductionPercent is > 0) quantExtra += $" QV:{finding.CrossSectionReductionPercent}%";
        return $"{clock} / {extent}{quantExtra} - {baseText}";
    }
}
