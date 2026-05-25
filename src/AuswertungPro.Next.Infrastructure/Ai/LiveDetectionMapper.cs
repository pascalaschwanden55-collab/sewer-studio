using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai;

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
                BboxX1: f.BboxX1,
                BboxY1: f.BboxY1,
                BboxX2: f.BboxX2,
                BboxY2: f.BboxY2));
        }

        // Bei schlechter Bildqualitaet: Severity leicht abstufen als Warnung,
        // Top-10 Findings durchlassen (genug fuer alle realen Schaeden, verhindert UI-Overflow).
        // Sewer-Videos sind generell dunkel/komprimiert — harter Filter (.Take(2)) verwarf zu viel.
        if (string.Equals(enhanced.ImageQuality, "schlecht", StringComparison.OrdinalIgnoreCase))
        {
            findings = findings
                .Select(f =>
                {
                    var code = NormalizeFindingCode(f.VsaCodeHint)
                               ?? InferCodeFromLabel(f.Label);
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

    private static string? NormalizeFindingCode(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return null;

        var normalized = Regex.Replace(rawCode.Trim().ToUpperInvariant(), @"[^A-Z0-9]", "");
        if (normalized.Length < 3)
            return null;

        return normalized[..3];
    }

    private static string? InferCodeFromLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var text = label.ToLowerInvariant();

        if (Has(text, "anschluss") || Has(text, "abzweig") || Has(text, "stutzen")
            || Has(text, "zulauf") || Has(text, "lateral connection") || HasWord(text, "lateral"))
            return "BCA";
        if (Has(text, "bogen") || Has(text, "kruemm") || Has(text, "kurve") || HasWord(text, "bend"))
            return "BCC";
        if (Has(text, "rohranfang") || Has(text, "pipe start") || Has(text, "anfangsknoten")
            || Has(text, "einstieg") || HasWord(text, "manhole"))
            return "BCD";
        if (Has(text, "rohrende") || Has(text, "pipe end") || Has(text, "endknoten") || Has(text, "ausstieg"))
            return "BCE";

        if (Has(text, "riss") || HasWord(text, "crack") || Has(text, "fracture") || Has(text, "fissure"))
            return "BAB";
        if (Has(text, "bruch") || Has(text, "einsturz") || Has(text, "collapse"))
            return "BAC";
        if (Has(text, "deformation") || Has(text, "verformung") || HasWord(text, "oval"))
            return "BAA";
        if (Has(text, "versatz") || HasWord(text, "offset") || Has(text, "displaced"))
            return "BAH";
        if (Has(text, "einragung") || Has(text, "intrusion") || Has(text, "protruding"))
            return "BAI";

        if (Has(text, "korrosion") || Has(text, "corrosion") || HasWord(text, "rost") || Has(text, "erosion"))
            return "BAJ";
        if (Has(text, "wurzel") || Has(text, "root intrusion") || Has(text, "bewuchs"))
            return "BBB";
        if (Has(text, "inkrustation") || Has(text, "encrustation") || Has(text, "kalk")
            || Has(text, "anhaftung") || Has(text, "sinter") || Has(text, "attached deposit"))
            return "BBA";
        if (Has(text, "ablagerung") || HasWord(text, "sediment") || Has(text, "schlamm")
            || HasWord(text, "silt") || HasWord(text, "debris"))
            return "BBC";
        if (Has(text, "wasserspiegel") || Has(text, "wasserstand") || Has(text, "wasserlinie")
            || Has(text, "water level") || Has(text, "waterline") || Has(text, "standing water")
            || HasWord(text, "puddle") || Has(text, "rueckstau")
            || (HasWord(text, "water")
                && (HasWord(text, "level") || HasWord(text, "standing") || Has(text, "sohle") || Has(text, "invert"))))
            return "BWB";

        return null;
    }

    private static bool Has(string text, string term) => text.Contains(term, StringComparison.Ordinal);

    private static bool HasWord(string text, string word)
        => Regex.IsMatch(text, $@"(^|[^a-z]){Regex.Escape(word)}([^a-z]|$)", RegexOptions.IgnoreCase);
}
