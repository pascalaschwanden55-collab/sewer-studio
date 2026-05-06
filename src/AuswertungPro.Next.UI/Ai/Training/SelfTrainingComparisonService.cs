// AuswertungPro – Vergleichslogik KI-Erkennung vs. Protokoll (deterministisch)
using System;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai.Shared;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Services.CodeCatalog;

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
    /// <param name="isPdfPhoto">True wenn das Bild aus einem PDF-Bildbericht stammt (kein OSD vorhanden).</param>
    ComparisonResult Compare(GroundTruthEntry truth, EnhancedFrameAnalysis analysis, bool isPdfPhoto = false);
}

public sealed class SelfTrainingComparisonService : ISelfTrainingComparisonService
{
    // Toleranzen
    private const double MeterTolerance = MeterTolerances.SelfTrainingComparison; // ± 1.0m
    private const int ClockTolerance = 1;            // ± 1 Stunde
    private const int SeverityTolerance = 1;         // ± 1 Stufe

    // Grundgeruest-Codes die KEIN "Schaden" sind — Qwen gibt dafuer meist findings=[] zurueck.
    // Grundgeruest-Codes: visuell "kein Schaden", KI darf findings=[] zurueckgeben.
    // NUR Codes die im Prompt definiert sind UND keine sichtbaren Schaeden zeigen.
    // KEINE AE-Codes (die muessen im Prompt separat erkannt werden).
    // KEINE BDBA-BDBE (das sind BDB mit Anmerkung, werden als eigene Codes gefuehrt).
    private static readonly HashSet<string> _basicStructureCodes = new(StringComparer.OrdinalIgnoreCase)
        { "BCD", "BCE", "BCC", "BDA", "BDB", "BDC", "BDD", "BDBA" };

    public ComparisonResult Compare(GroundTruthEntry truth, EnhancedFrameAnalysis analysis, bool isPdfPhoto = false)
    {
        if (!analysis.HasFindings)
        {
            // Grundgeruest-Codes (BCD, BCE, BDB, BDA, AE* etc.) sind keine Schaeden.
            // Wenn Qwen nichts findet UND der Protokolleintrag ein solcher Code ist →
            // das ist KORREKT (normaler Rohrblick, Materialwechsel, Steuercode).
            // Gilt fuer alle Quellen (Video + PDF), nicht nur PDF.
            var truthNorm = truth.VsaCode.Replace(".", "", StringComparison.Ordinal).ToUpperInvariant();
            bool isStructure = _basicStructureCodes.Contains(truthNorm);
            // Auch Praefix-Match: AEDXO → AED ist in der Liste
            if (!isStructure && truthNorm.Length > 3)
                isStructure = _basicStructureCodes.Contains(truthNorm[..3]);
            if (!isStructure && truthNorm.Length > 2)
                isStructure = _basicStructureCodes.Contains(truthNorm[..2]);

            if (isStructure)
            {
                bool meterOk = (isPdfPhoto && !analysis.Meter.HasValue)
                    || MeterMatches(truth.MeterStart, analysis.Meter);
                return new ComparisonResult(
                    Level: meterOk ? MatchLevel.ExactMatch : MatchLevel.PartialMatch,
                    ConfidenceScore: 0.70,
                    Explanation: $"Grundgeruest {truth.VsaCode} @ {truth.MeterStart:F1}m — KI korrekt: keine Schaeden erkannt.",
                    CodeMatched: true,
                    MeterMatched: meterOk,
                    SeverityPlausible: true,
                    ClockMatched: true,
                    BestMatchCode: truth.VsaCode,
                    BestMatchMeter: analysis.Meter);
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
            // Code-Matching: 4-stufiger Fallback
            // 0. Label direkt als VSA-Code normalisieren (seit Prompt label=Code fordert,
            //    kann finding.Label direkt "BABBA" enthalten — das muss erkannt werden)
            // 1. vsa_code_hint direkt aus Qwen (z.B. "BABBA")
            // 2. InferCodeFromLabel: Label-Text → Code (z.B. "Riss" → "BAB")
            // 3. ReverseLookup: Langtext → Code (z.B. "Anschluss mit Formstück" → "BCAAA")
            string? resolvedCode = finding.VsaCodeHint;
            if (string.IsNullOrEmpty(resolvedCode) && !string.IsNullOrEmpty(finding.Label))
            {
                // Schritt 0: Label selbst koennte ein gueltiger VSA-Code sein (z.B. "BABBA")
                resolvedCode = VsaCodeResolver.NormalizeFindingCode(finding.Label)
                    ?? VsaCodeResolver.InferCodeFromLabel(finding.Label)
                    ?? VsaCodeTree.ReverseLookup(finding.Label);
            }
            bool codeMatch = CodesMatch(truth.VsaCode, resolvedCode);

            // Bei PDF-Fotos: Meter ist implizit korrekt (Foto gehoert zum Protokolleintrag)
            bool meterMatch = isPdfPhoto && !analysis.Meter.HasValue
                ? true
                : MeterMatches(truth.MeterStart, analysis.Meter);

            bool severityOk = SeverityPlausible(truth.VsaCode, finding.Severity);

            // Bei PDF-Fotos: Clock nicht bestrafen wenn KI keine Uhrlage liefert
            bool clockMatch = isPdfPhoto
                ? ClockMatchesPdfTolerant(truth.ClockPosition, finding.PositionClock)
                : ClockMatches(truth.ClockPosition, finding.PositionClock);

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

        // BestMatchCode: den aufgeloesten Code zurueckgeben (nicht nur VsaCodeHint)
        string? bestResolvedCode = bestMatch?.VsaCodeHint;
        if (string.IsNullOrEmpty(bestResolvedCode) && bestMatch is not null && !string.IsNullOrEmpty(bestMatch.Label))
        {
            bestResolvedCode = VsaCodeResolver.InferCodeFromLabel(bestMatch.Label)
                ?? VsaCodeTree.ReverseLookup(bestMatch.Label);
        }

        return new ComparisonResult(
            Level: level,
            ConfidenceScore: Math.Round(bestScore, 2),
            Explanation: explanation,
            CodeMatched: bestCodeMatch,
            MeterMatched: bestMeterMatch,
            SeverityPlausible: bestSeverityOk,
            ClockMatched: bestClockMatch,
            BestMatchCode: bestResolvedCode,
            BestMatchMeter: analysis.Meter);
    }

    // ── Code-Vergleich ──

    /// <summary>
    /// Vergleicht VSA-Codes. Beruecksichtigt:
    /// - Exakte Uebereinstimmung
    /// - Praefix-Match (z.B. "BAB" matcht "BABA" = Laengsriss)
    /// - Gleiche 3-Zeichen-Gruppe NUR wenn beide mindestens 4 Zeichen haben
    ///   (verhindert dass "BAB" Riss und "BAC" Bruch matchen)
    /// </summary>
    private static bool CodesMatch(string truthCode, string? kiCode)
    {
        if (string.IsNullOrEmpty(kiCode)) return false;

        // Punkt-Notation entfernen: "BDC.A" → "BDC", "BAB.B" → "BAB"
        string t = truthCode.ToUpperInvariant().Trim().Split('.')[0];
        string k = kiCode.ToUpperInvariant().Trim().Split('.')[0];

        // Exakt
        if (t == k) return true;

        // Praefix-Match: "BAB" matcht "BABA" (spezifischere Variante des gleichen Codes)
        // Mindestens 3 Zeichen muessen uebereinstimmen um Fehlmatches zu vermeiden
        if (k.Length > t.Length && t.Length >= 3 && k.StartsWith(t, StringComparison.Ordinal)) return true;
        if (t.Length > k.Length && k.Length >= 3 && t.StartsWith(k, StringComparison.Ordinal)) return true;

        // Gleiche 3-Zeichen-Gruppe NUR wenn beide Codes laenger sind (= spezifischere Varianten)
        // "BABA" und "BABB" → gleiche Gruppe BAB → Match (beides Riss-Untertypen)
        // "BAB" und "BAC" → KEIN Match (verschiedene Schadensarten auf 3-Zeichen-Ebene)
        if (t.Length >= 4 && k.Length >= 4 && t[..3] == k[..3]) return true;

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
    /// BA (Strukturell): Severity 2-5 (1 = "optisch" kommt bei Strukturschaeden nicht vor)
    /// BB (Betrieblich): Severity 1-4 (5 = kritisch bei betrieblichen Stoerungen unueblich)
    /// BC/BD (Inventar/Steuer): immer plausibel
    /// </summary>
    private static bool SeverityPlausible(string truthCode, int kiSeverity)
    {
        if (kiSeverity < 1 || kiSeverity > 5) return false;

        string upper = truthCode.ToUpperInvariant();
        if (upper.Length < 2) return true; // Nicht genug Info

        char category = upper.Length >= 2 ? upper[1] : ' ';
        return category switch
        {
            'A' => kiSeverity >= 2 && kiSeverity <= 5, // Strukturell: 2-5 (kein rein optischer Strukturschaden)
            'B' => kiSeverity >= 1 && kiSeverity <= 4, // Betrieblich: 1-4 (Severity 5 bei BB unueblich)
            'C' => true, // Inventar: alle plausibel
            'D' => true, // Steuer: alle plausibel
            _ => true
        };
    }

    // ── Uhrzeigerposition-Vergleich ──

    /// <summary>
    /// Toleranterer Clock-Vergleich fuer PDF-Fotos:
    /// Wenn KI keine Uhrlage liefert, wird das nicht bestraft (= true).
    /// PDF-Fotos zeigen oft nicht genug Kontext fuer eine Uhrlage.
    /// </summary>
    private static bool ClockMatchesPdfTolerant(string? truthClock, string? kiClock)
    {
        // Beide leer = Match
        if (string.IsNullOrEmpty(truthClock) && string.IsNullOrEmpty(kiClock)) return true;
        // KI hat keine Uhrlage → bei PDF-Fotos nicht bestrafen
        if (string.IsNullOrEmpty(kiClock)) return true;
        // Protokoll hat keine Uhrlage, KI schon → auch ok
        if (string.IsNullOrEmpty(truthClock)) return true;
        // Beide vorhanden → normal vergleichen
        if (!TryParseClock(truthClock, out int tHour)) return true;
        if (!TryParseClock(kiClock, out int kHour)) return true;
        int diff = Math.Abs(tHour - kHour);
        if (diff > 6) diff = 12 - diff;
        return diff <= ClockTolerance;
    }

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
