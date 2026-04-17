// AuswertungPro – Video-Selbsttraining Phase 2
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Vergleicht KI-Blinddetektionen gegen importierte Protokolleintraege (Ground-Truth).
/// Erzeugt einen DifferenceReport mit True Positives, False Negatives, False Positives und Code-Mismatches.
///
/// Algorithmus: Greedy-Assignment mit Meter-Toleranz und Code-Matching.
/// </summary>
public static class DifferenceAnalyzer
{
    // VSA-Code-Hauptgruppen (erste 3 Zeichen) fuer Gruppen-Matching
    private static readonly HashSet<string> GrundgeruestCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BCD", "BCE", "BCC", "BDA", "BDB", "BDC", "BDD", "BDBA", "BCDXP", "BCEXP"
    };

    /// <summary>
    /// Fuehrt den Differenzvergleich durch.
    /// </summary>
    /// <param name="groundTruths">Protokolleintraege (Ground-Truth).</param>
    /// <param name="detections">KI-Detektionen aus dem Blinddurchlauf.</param>
    /// <param name="meterTolerance">Maximale Meter-Abweichung fuer Zuordnung (Default: 0.5m).</param>
    public static DifferenceReport Analyze(
        IReadOnlyList<GroundTruthEntry> groundTruths,
        IReadOnlyList<BlindDetection> detections,
        double meterTolerance = 0.5)
    {
        var entries = new List<DifferenceEntry>();

        // Kopie der Detektionen mit Assignment-Flag
        var availableDetections = detections.ToList();
        foreach (var d in availableDetections)
            d.IsAssigned = false;

        // Phase 1: Fuer jeden Protokolleintrag den besten KI-Match finden
        foreach (var gt in groundTruths)
        {
            // Grundgeruest-Codes (BCD, BCE etc.) erzeugen normalerweise keine KI-Detektionen
            // — sie sind Steuercodes, keine Schaeden. Wenn kein Match → kein FN.
            var isGrundgeruest = IsGrundgeruestCode(gt.VsaCode);

            // Kandidaten: Alle nicht-zugeordneten Detektionen innerhalb der Meter-Toleranz
            var candidates = availableDetections
                .Where(d => !d.IsAssigned && Math.Abs(GetDetectionMeter(d) - GetTruthMeter(gt)) <= meterTolerance)
                .ToList();

            if (candidates.Count == 0)
            {
                if (isGrundgeruest)
                {
                    // Grundgeruest-Code ohne KI-Match → erwartetes Verhalten (TP)
                    entries.Add(new DifferenceEntry
                    {
                        Category = DifferenceCategory.TruePositive,
                        ProtocolEntry = gt,
                        KiDetection = null,
                        FramePath = null,
                        Explanation = $"Steuercode {gt.VsaCode} — KI hat korrekt keinen Schaden erkannt."
                    });
                }
                else
                {
                    // Echter Schaden ohne KI-Match → False Negative
                    entries.Add(new DifferenceEntry
                    {
                        Category = DifferenceCategory.FalseNegative,
                        ProtocolEntry = gt,
                        KiDetection = null,
                        FramePath = null,
                        Explanation = $"Schaden {gt.VsaCode} @ {gt.MeterStart:F1}m von KI nicht erkannt."
                    });
                }
                continue;
            }

            // Besten Kandidaten auswaehlen (hoechster Score)
            var scored = candidates
                .Select(d => (Detection: d, Score: ScoreMatch(gt, d)))
                .OrderByDescending(x => x.Score)
                .First();

            var best = scored.Detection;
            var score = scored.Score;

            // Code-Vergleich
            var codeMatched = CodesMatch(gt.VsaCode, best.VsaCode, best.Label);

            DifferenceCategory category;
            string explanation;

            if (codeMatched && score >= 0.40)
            {
                category = DifferenceCategory.TruePositive;
                explanation = $"Treffer: {gt.VsaCode} @ {gt.MeterStart:F1}m ↔ KI: {best.VsaCode ?? best.Label} @ {best.Meter:F1}m (Score {score:F2})";
            }
            else if (!codeMatched && score >= 0.25)
            {
                category = DifferenceCategory.CodeMismatch;
                explanation = $"Code-Abweichung: Protokoll={gt.VsaCode}, KI={best.VsaCode ?? best.Label} @ {best.Meter:F1}m (Score {score:F2})";
            }
            else
            {
                // Score zu niedrig — als FN behandeln, Detektion bleibt verfuegbar
                entries.Add(new DifferenceEntry
                {
                    Category = DifferenceCategory.FalseNegative,
                    ProtocolEntry = gt,
                    KiDetection = null,
                    FramePath = null,
                    Explanation = $"Schaden {gt.VsaCode} @ {gt.MeterStart:F1}m — kein ausreichender KI-Match (bester Score: {score:F2})."
                });
                continue;
            }

            best.IsAssigned = true;
            entries.Add(new DifferenceEntry
            {
                Category = category,
                ProtocolEntry = gt,
                KiDetection = best,
                FramePath = best.FramePath,
                Explanation = explanation,
                MatchConfidenceScore = score
            });
        }

        // Phase 2: Nicht-zugeordnete KI-Detektionen → False Positives
        foreach (var det in availableDetections.Where(d => !d.IsAssigned))
        {
            // Grundgeruest-Labels (z.B. "pipe_start", "pipe_end") sind keine echten FP
            if (IsGrundgeruestLabel(det.Label))
                continue;

            entries.Add(new DifferenceEntry
            {
                Category = DifferenceCategory.FalsePositive,
                ProtocolEntry = null,
                KiDetection = det,
                FramePath = det.FramePath,
                Explanation = $"KI-Fund ohne Protokoll-Gegenstueck: {det.VsaCode ?? det.Label} @ {det.Meter:F1}m"
            });
        }

        return new DifferenceReport { Entries = entries };
    }

    /// <summary>
    /// Berechnet einen Score (0-1) fuer die Uebereinstimmung zwischen GroundTruth und Detektion.
    /// Gewichtung: Code 0.40, Meter 0.30, Severity 0.15, Clock 0.15.
    /// </summary>
    private static double ScoreMatch(GroundTruthEntry gt, BlindDetection det)
    {
        double score = 0;

        // Code-Match (0.40)
        if (CodesMatch(gt.VsaCode, det.VsaCode, det.Label))
            score += 0.40;
        else if (SameCodeGroup(gt.VsaCode, det.VsaCode))
            score += 0.20; // Gleiche Hauptgruppe (z.B. BA* vs BA*) — Teilpunkt

        // Meter-Naehe (0.30) — naeher = besser, Toleranz bis 2m (OSD-Genauigkeit)
        var meterDelta = Math.Abs(GetTruthMeter(gt) - GetDetectionMeter(det));
        score += 0.30 * Math.Max(0, 1.0 - meterDelta / 2.0); // 0m → 0.30, 1m → 0.15, 2m → 0

        // Severity-Plausibilitaet (0.15)
        if (det.Severity > 0 && gt.VsaCode.Length >= 3)
        {
            // Severity-Differenz ≤ 1 → volle Punkte
            // Wir schaetzen die erwartete Severity nicht, da das Protokoll keinen Severity-Wert hat
            // Stattdessen: Severity > 0 → Basis-Punkte
            score += 0.10;
        }

        // Clock-Match (0.15) — nur wenn beide angegeben
        if (!string.IsNullOrEmpty(gt.ClockPosition) && !string.IsNullOrEmpty(det.ClockPosition))
        {
            if (ClocksMatch(gt.ClockPosition, det.ClockPosition))
                score += 0.15;
        }
        else
        {
            // Wenn eine Seite keine Uhrlage hat → neutral (halbe Punkte)
            score += 0.075;
        }

        return Math.Round(score, 3);
    }

    /// <summary>
    /// Prueft ob VSA-Codes uebereinstimmen (exakt oder Praefix-Match).
    /// Nutzt auch Label-Inferenz (z.B. "crack" → BAB).
    /// </summary>
    private static bool CodesMatch(string truthCode, string? detCode, string? detLabel)
    {
        if (string.IsNullOrEmpty(truthCode)) return false;

        var truth = NormalizeCode(truthCode);

        // 1. Direkter Code-Vergleich
        if (!string.IsNullOrEmpty(detCode))
        {
            var det = NormalizeCode(detCode);
            if (string.Equals(truth, det, StringComparison.OrdinalIgnoreCase))
                return true;
            // Praefix-Match: "BAB" matcht "BABBA"
            if (det.StartsWith(truth, StringComparison.OrdinalIgnoreCase) ||
                truth.StartsWith(det, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 2. Label-Inferenz (z.B. "Riss" → BAB)
        if (!string.IsNullOrEmpty(detLabel))
        {
            var inferred = VsaCodeResolver.InferCodeFromLabel(detLabel);
            if (!string.IsNullOrEmpty(inferred))
            {
                var inf = NormalizeCode(inferred);
                if (string.Equals(truth, inf, StringComparison.OrdinalIgnoreCase) ||
                    inf.StartsWith(truth, StringComparison.OrdinalIgnoreCase) ||
                    truth.StartsWith(inf, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Prueft ob zwei Codes in der gleichen 3-Zeichen-Hauptgruppe liegen.</summary>
    private static bool SameCodeGroup(string code1, string? code2)
    {
        if (string.IsNullOrEmpty(code2) || code1.Length < 3 || code2.Length < 3) return false;
        return string.Equals(
            code1[..3], code2[..3],
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Prueft ob zwei Uhrlagen-Angaben uebereinstimmen (±1 Stunde).</summary>
    private static bool ClocksMatch(string clock1, string clock2)
    {
        if (!int.TryParse(clock1.Replace("Uhr", "").Replace(":00", "").Trim(), out var h1))
            return false;
        if (!int.TryParse(clock2.Replace("Uhr", "").Replace(":00", "").Trim(), out var h2))
            return false;

        // Zirkulaere Differenz (12-Stunden-Uhr)
        var diff = Math.Abs(h1 - h2);
        if (diff > 6) diff = 12 - diff;
        return diff <= 1;
    }

    /// <summary>Normalisiert einen VSA-Code: Punkte entfernen, Grossbuchstaben.</summary>
    private static string NormalizeCode(string code) =>
        code.Replace(".", "").Trim().ToUpperInvariant();

    /// <summary>Prueft ob ein VSA-Code ein Grundgeruest-/Steuercode ist.</summary>
    private static bool IsGrundgeruestCode(string code)
    {
        var norm = NormalizeCode(code);
        if (GrundgeruestCodes.Contains(norm)) return true;
        // Praefix-Match: BCDXP, BCEXP, BDB*, BDC*, AE* etc.
        return norm.StartsWith("BCD", StringComparison.OrdinalIgnoreCase) ||
               norm.StartsWith("BCE", StringComparison.OrdinalIgnoreCase) ||
               norm.StartsWith("BDB", StringComparison.OrdinalIgnoreCase) ||
               norm.StartsWith("BDC", StringComparison.OrdinalIgnoreCase) ||
               norm.StartsWith("BDA", StringComparison.OrdinalIgnoreCase) ||
               norm.StartsWith("AE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Prueft ob ein KI-Label auf ein Grundgeruest-Element hindeutet.</summary>
    private static bool IsGrundgeruestLabel(string? label)
    {
        if (string.IsNullOrEmpty(label)) return false;
        var l = label.ToLowerInvariant();
        return l.Contains("pipe_start") || l.Contains("pipe_end") ||
               l.Contains("rohranfang") || l.Contains("rohrende") ||
               l.Contains("manhole") || l.Contains("schacht");
    }

    /// <summary>Meterstand aus GroundTruthEntry (Mitte bei Streckenschaden).</summary>
    private static double GetTruthMeter(GroundTruthEntry gt) =>
        gt.IsStreckenschaden ? (gt.MeterStart + gt.MeterEnd) / 2.0 : gt.MeterStart;

    /// <summary>Meterstand aus BlindDetection.</summary>
    private static double GetDetectionMeter(BlindDetection det) => det.Meter;
}
