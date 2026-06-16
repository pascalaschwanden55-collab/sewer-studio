// AuswertungPro – Vergleichslogik KI-Erkennung vs. Protokoll (deterministisch)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

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
    private const int ClockTolerance = 1;            // ± 1 Stunde
    // SeverityTolerance nicht als Konstante — Plausibilitaet ist kategorieabhaengig (siehe SeverityPlausible)
    // Meter-Toleranz ist schadenstyp-abhaengig (siehe MeterToleranceFor):
    private const double AnschlussMeterTolerance = 0.30;  // BCA/BAH: muss genau sitzen
    private const double DefaultMeterTolerance = 0.50;    // Einzelschaden / unbekannt
    private const double StreckenEdgeTolerance = 0.50;    // Rand-Toleranz bei Overlap-Pruefung

    public ComparisonResult Compare(GroundTruthEntry truth, EnhancedFrameAnalysis analysis)
    {
        if (!analysis.HasFindings)
        {
            if (!analysis.IsTrainableNegative)
            {
                var reason = string.IsNullOrWhiteSpace(analysis.Error)
                    ? analysis.Outcome.ToString()
                    : $"{analysis.Outcome}: {analysis.Error}";
                return new ComparisonResult(
                    Level: MatchLevel.Mismatch,
                    ConfidenceScore: 0.0,
                    Explanation: $"KI-Analyse nicht als echter Negativbefund wertbar ({reason}).",
                    CodeMatched: false,
                    MeterMatched: false,
                    SeverityPlausible: false,
                    ClockMatched: false,
                    BestMatchCode: null,
                    BestMatchMeter: null);
            }

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
            bool meterMatch = MeterMatches(truth, analysis.Meter);
            bool severityOk = SeverityPlausible(truth.VsaCode, finding.Severity);
            // Nur eine positiv bestaetigte Uhrlage (Protokoll hat + KI gleich) zaehlt. Fehlende
            // Protokoll-Uhrlage erzeugt KEINEN Volltreffer (Neutral/OverClaim/Conflict != Match).
            bool clockMatch = EvaluateClock(truth.ClockPosition, finding.PositionClock) == ClockEval.Match;

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

        // Match-Level bestimmen. ExactMatch (= Grundlage fuer Auto-Accept im Orchestrator) verlangt
        // jetzt ALLE Achsen sauber: Code exakt, Meter in (typabhaengiger) Toleranz, Severity plausibel
        // UND positiv bestaetigte Uhrlage. Alles andere -> Partial/Mismatch -> ReviewQueue statt Gold.
        MatchLevel level;
        if (bestCodeMatch && bestMeterMatch && bestSeverityOk && bestClockMatch)
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

    /// <summary>
    /// Meter-Abgleich mit schadenstyp-abhaengiger Toleranz:
    /// - Streckenschaden (MeterEnd&gt;MeterStart): Overlap-Pruefung (KI-Punkt im Bereich +/- Rand) statt Punktdistanz.
    /// - Anschluss/Zulauf/Abzweiger (BCA*/BAH*): +/- 0.30 m (muss genau sitzen).
    /// - sonst (Einzelschaden/unbekannt): +/- 0.50 m.
    /// </summary>
    private static bool MeterMatches(GroundTruthEntry truth, double? kiMeter)
    {
        if (!kiMeter.HasValue) return false;
        var ki = kiMeter.Value;

        if (truth.IsStreckenschaden && truth.MeterEnd > truth.MeterStart)
            return ki >= truth.MeterStart - StreckenEdgeTolerance
                && ki <= truth.MeterEnd + StreckenEdgeTolerance;

        return Math.Abs(truth.MeterStart - ki) <= MeterToleranceFor(truth.VsaCode);
    }

    private static double MeterToleranceFor(string? vsaCode)
    {
        var c = (vsaCode ?? string.Empty).ToUpperInvariant().Trim();
        // BCA = seitlicher Anschluss, BAH = schadhafter Anschluss -> Position muss genau sitzen.
        if (c.StartsWith("BCA", StringComparison.Ordinal) || c.StartsWith("BAH", StringComparison.Ordinal))
            return AnschlussMeterTolerance;
        return DefaultMeterTolerance;
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

    /// <summary>Drei(+1)-Wert-Bewertung der Uhrlage Protokoll vs. KI.</summary>
    private enum ClockEval
    {
        Match,      // Protokoll hat Uhrlage UND KI gleiche -> einziger Fall der ExactMatch erlaubt
        Conflict,   // Protokoll hat Uhrlage, KI weicht ab ODER fehlt -> Review, kein ExactMatch
        Neutral,    // beide leer -> unbewertet, kein Pluspunkt, kein Volltreffer
        OverClaim   // Protokoll leer, KI gibt Uhrlage an -> kein harter Fehler, aber kein ExactMatch
    }

    /// <summary>
    /// Bewertet die Uhrlage. WICHTIG: Eine fehlende Protokoll-Uhrlage darf keinen Volltreffer
    /// erzeugen — nur <see cref="ClockEval.Match"/> zaehlt fuer ExactMatch/Score.
    /// </summary>
    private static ClockEval EvaluateClock(string? truthClock, string? kiClock)
    {
        bool truthHas = !string.IsNullOrWhiteSpace(truthClock);
        bool kiHas = !string.IsNullOrWhiteSpace(kiClock);

        if (truthHas && kiHas)
            return ClocksEqual(truthClock!, kiClock!) ? ClockEval.Match : ClockEval.Conflict;
        if (truthHas)            // Protokoll hat Uhrlage, KI leer -> Review
            return ClockEval.Conflict;
        if (!kiHas)              // beide leer -> unbewertet
            return ClockEval.Neutral;
        return ClockEval.OverClaim; // Protokoll leer, KI gibt Uhrlage an
    }

    private static bool ClocksEqual(string truthClock, string kiClock)
    {
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
