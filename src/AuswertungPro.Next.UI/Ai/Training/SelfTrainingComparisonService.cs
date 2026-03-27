// AuswertungPro – Vergleichslogik KI-Erkennung vs. Protokoll (deterministisch)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Vergleicht KI-Erkennungen (EnhancedFrameAnalysis) mit Ground-Truth-Eintraegen aus dem Protokoll.
/// Rein deterministisch, kein LLM-Aufruf.
/// </summary>
public interface ISelfTrainingComparisonService
{
    /// <summary>
    /// Vergleicht einen Protokolleintrag mit der blinden KI-Analyse eines Frames.
    /// </summary>
    ComparisonResult Compare(GroundTruthEntry truth, EnhancedFrameAnalysis analysis);
}

public sealed class SelfTrainingComparisonService : ISelfTrainingComparisonService
{
    // Toleranzen
    private const double MeterTolerance = 1.0;      // ± 1.0m
    private const int ClockTolerance = 1;            // ± 1 Stunde
    // SeverityTolerance nicht als Konstante — Plausibilitaet ist kategorieabhaengig (siehe SeverityPlausible)

    public ComparisonResult Compare(GroundTruthEntry truth, EnhancedFrameAnalysis analysis)
    {
        if (!analysis.HasFindings)
        {
            return new ComparisonResult(
                Level: MatchLevel.NoFindings,
                ConfidenceScore: 0.0,
                Explanation: $"KI hat keine Befunde bei {truth.MeterStart:F1}m erkannt.",
                CodeMatched: false,
                MeterMatched: false,
                SeverityPlausible: false,
                ClockMatched: false,
                BestMatchCode: null,
                BestMatchMeter: null);
        }

        // Beste Uebereinstimmung finden
        EnhancedFinding? bestMatch = null;
        double bestScore = -1;
        bool bestCodeMatch = false;
        bool bestMeterMatch = false;
        bool bestSeverityOk = false;
        bool bestClockMatch = false;

        foreach (var finding in analysis.Findings)
        {
            bool codeMatch = CodesMatch(truth.VsaCode, finding.VsaCodeHint);
            bool meterMatch = MeterMatches(truth.MeterStart, analysis.Meter);
            bool severityOk = SeverityPlausible(truth.VsaCode, finding.Severity);
            bool clockMatch = ClockMatches(truth.ClockPosition, finding.PositionClock);

            // Gewichtete Punktzahl
            double score = 0;
            if (codeMatch) score += 0.40;
            if (meterMatch) score += 0.25;
            if (severityOk) score += 0.15;
            if (clockMatch) score += 0.20;

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = finding;
                bestCodeMatch = codeMatch;
                bestMeterMatch = meterMatch;
                bestSeverityOk = severityOk;
                bestClockMatch = clockMatch;
            }
        }

        // Match-Level bestimmen
        MatchLevel level;
        if (bestCodeMatch && bestMeterMatch && bestClockMatch)
            level = MatchLevel.ExactMatch;
        else if (bestCodeMatch)
            level = MatchLevel.PartialMatch;
        else
            level = MatchLevel.Mismatch;

        string explanation = BuildExplanation(truth, bestMatch!, level, bestCodeMatch, bestMeterMatch, bestClockMatch);

        return new ComparisonResult(
            Level: level,
            ConfidenceScore: Math.Round(bestScore, 2),
            Explanation: explanation,
            CodeMatched: bestCodeMatch,
            MeterMatched: bestMeterMatch,
            SeverityPlausible: bestSeverityOk,
            ClockMatched: bestClockMatch,
            BestMatchCode: bestMatch?.VsaCodeHint,
            BestMatchMeter: analysis.Meter);
    }

    // ── Code-Vergleich ──

    /// <summary>
    /// Vergleicht VSA-Codes. Beruecksichtigt:
    /// - Exakte Uebereinstimmung
    /// - Praefix-Match (nur Protokoll-Code als Praefix der KI-Erkennung, nicht umgekehrt)
    /// Kein Gruppenvergleich — zu viele False Positives.
    /// </summary>
    private static bool CodesMatch(string truthCode, string? kiCode)
    {
        if (string.IsNullOrEmpty(kiCode)) return false;

        // Punkt-Notation entfernen: "BDC.A" → "BDC", "BAB.B" → "BAB"
        string t = truthCode.ToUpperInvariant().Trim().Split('.')[0];
        string k = kiCode.ToUpperInvariant().Trim().Split('.')[0];

        // Exakt
        if (t == k) return true;

        // Praefix: Protokoll "BAB" matcht KI "BABA" (KI spezifischer als Protokoll = ok)
        // Umgekehrt NICHT: KI "BA" soll nicht Protokoll "BAB" matchen
        if (k.StartsWith(t, StringComparison.Ordinal) && k.Length <= t.Length + 2) return true;

        return false;
    }

    // ── Meter-Vergleich ──

    private static bool MeterMatches(double truthMeter, double? kiMeter)
    {
        if (!kiMeter.HasValue) return false;
        return Math.Abs(truthMeter - kiMeter.Value) <= MeterTolerance;
    }

    // ── Schweregrad-Plausibilitaet ──

    /// <summary>
    /// Prueft ob der KI-Schweregrad zum VSA-Code plausibel ist.
    /// VSA-Kategorien: BA = baulich/strukturell (typisch 2-5),
    /// BB = betrieblich (typisch 1-3), BC = Inventar (typisch 1-2).
    /// </summary>
    private static bool SeverityPlausible(string truthCode, int kiSeverity)
    {
        if (kiSeverity < 1 || kiSeverity > 5) return false;

        string upper = truthCode.ToUpperInvariant();
        if (upper.Length < 2) return true; // Nicht genug Info

        char category = upper.Length >= 2 ? upper[1] : ' ';
        return category switch
        {
            // Baulich/strukturell: Risse, Deformationen, Brueche → ernst, Severity 2-5
            'A' => kiSeverity >= 2,
            // Betrieblich: Ablagerungen, Wurzeln, Hindernisse → leicht bis mittel, Severity 1-4
            'B' => kiSeverity <= 4,
            // Inventar: Anschluesse, Einbauten → niedrig, Severity 1-2
            'C' => kiSeverity <= 2,
            _ => true
        };
    }

    // ── Uhrzeigerposition-Vergleich ──

    private static bool ClockMatches(string? truthClock, string? kiClock)
    {
        // Beide leer = kein Uhrzeitvergleich noetig = Match
        if (string.IsNullOrEmpty(truthClock) && string.IsNullOrEmpty(kiClock)) return true;
        if (string.IsNullOrEmpty(truthClock) || string.IsNullOrEmpty(kiClock)) return false;

        if (!TryParseClock(truthClock, out int tHour)) return false;
        if (!TryParseClock(kiClock, out int kHour)) return false;

        // Zirkulaere Differenz (12-Stunden-Uhr)
        int diff = Math.Abs(tHour - kHour);
        if (diff > 6) diff = 12 - diff;

        return diff <= ClockTolerance;
    }

    private static bool TryParseClock(string clock, out int hour)
    {
        hour = 0;
        if (string.IsNullOrWhiteSpace(clock)) return false;

        // "3 Uhr" → 3, "03" → 3, "3" → 3, "12" → 12
        string cleaned = clock.Replace("Uhr", "", StringComparison.OrdinalIgnoreCase)
                              .Replace("h", "", StringComparison.OrdinalIgnoreCase)
                              .Trim();

        if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
        {
            hour = val % 12; // 12 → 0 intern, 1-11 bleiben
            if (val == 12) hour = 12; // 12 Uhr = oben
            return hour >= 0 && hour <= 12;
        }
        return false;
    }

    // ── Erklaerungstext ──

    private static string BuildExplanation(
        GroundTruthEntry truth, EnhancedFinding bestMatch,
        MatchLevel level, bool code, bool meter, bool clock)
    {
        var parts = new List<string>();

        string matchSymbol(bool ok) => ok ? "✓" : "✗";

        parts.Add($"Protokoll: {truth.VsaCode} @ {truth.MeterStart:F1}m");
        parts.Add($"KI: {bestMatch.VsaCodeHint ?? bestMatch.Label} (Sev={bestMatch.Severity})");

        var checks = new List<string>
        {
            $"Code {matchSymbol(code)}",
            $"Meter {matchSymbol(meter)}"
        };
        if (!string.IsNullOrEmpty(truth.ClockPosition))
            checks.Add($"Uhr {matchSymbol(clock)}");

        parts.Add(string.Join(" | ", checks));

        string levelText = level switch
        {
            MatchLevel.ExactMatch => "→ Volltreffer",
            MatchLevel.PartialMatch => "→ Teiltreffer",
            MatchLevel.Mismatch => "→ Abweichung",
            _ => "→ Keine Erkennung"
        };
        parts.Add(levelText);

        return string.Join(" · ", parts);
    }
}
