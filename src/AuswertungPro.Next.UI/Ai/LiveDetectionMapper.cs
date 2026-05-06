using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Gemeinsames Mapping fuer Enhanced/Qwen-Ergebnisse in das LiveDetection-Format.
/// So nutzen PlayerWindow und CodingModeWindow denselben Vertrag fuer
/// ImageQuality-Gate, Meteruebernahme und BBox-Weitergabe.
/// </summary>
public static class LiveDetectionMapper
{
    public static LiveDetection FromEnhancedAnalysis(
        EnhancedFrameAnalysis enhanced,
        double timestampSec)
    {
        if (enhanced.Error != null)
            return new LiveDetection(timestampSec, Array.Empty<LiveFrameFinding>(), null, enhanced.Error);

        var findings = new List<LiveFrameFinding>(enhanced.Findings.Count);
        foreach (var f in enhanced.Findings)
        {
            findings.Add(new LiveFrameFinding(
                Label: f.Label,
                Severity: f.Severity,
                PositionClock: f.PositionClock,
                ExtentPercent: f.ExtentPercent,
                VsaCodeHint: f.VsaCodeHint,
                HeightMm: f.HeightMm,
                WidthMm: f.WidthMm,
                IntrusionPercent: f.IntrusionPercent,
                CrossSectionReductionPercent: f.CrossSectionReductionPercent,
                DiameterReductionMm: f.DiameterReductionMm,
                BboxX1: f.BboxX1Norm,
                BboxY1: f.BboxY1Norm,
                BboxX2: f.BboxX2Norm,
                BboxY2: f.BboxY2Norm));
        }

        // Bei schlechter Bildqualitaet: Severity leicht abstufen als Warnung,
        // Top-10 Findings durchlassen (genug fuer alle realen Schaeden, verhindert UI-Overflow).
        // Sewer-Videos sind generell dunkel/komprimiert — harter Filter (.Take(2)) verwarf zu viel.
        if (string.Equals(enhanced.ImageQuality, "schlecht", StringComparison.OrdinalIgnoreCase))
        {
            findings = findings
                .Select(f =>
                {
                    var code = VsaCodeResolver.NormalizeFindingCode(f.VsaCodeHint)
                               ?? VsaCodeResolver.InferCodeFromLabel(f.Label);
                    if (code == null) return null;
                    return f with
                    {
                        VsaCodeHint = code,
                        Severity = Math.Max(1, f.Severity - 1)
                    };
                })
                .Where(f => f != null)
                .Select(f => f!)
                .OrderByDescending(f => f.Severity)
                .Take(10) // Max 10 Findings bei schlechter Qualitaet (UI-Overflow verhindern)
                .ToList();

            return new LiveDetection(timestampSec, findings, enhanced.Meter, null);
        }

        return new LiveDetection(timestampSec, findings, enhanced.Meter, null);
    }
}
