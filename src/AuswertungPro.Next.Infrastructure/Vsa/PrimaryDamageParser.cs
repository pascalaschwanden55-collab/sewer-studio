using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using VsaFinding = AuswertungPro.Next.Domain.Models.VsaFinding;

namespace AuswertungPro.Next.Infrastructure.Vsa;

/// <summary>
/// Reine Parse-Logik fuer den Primaerschaden-Freitext (Primaere_Schaeden-Feld).
/// Kein IO, kein State. Alle Methoden sind statisch und seiteneffektfrei.
/// </summary>
internal static class PrimaryDamageParser
{
    private static readonly Regex LeadingTokenRegex = new(@"^[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)*", RegexOptions.Compiled);
    private static readonly Regex PrimaryDamageLineRegex = new(
        @"^(?<code>[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)*)\s*(?:@(?<meter>-?\d+(?:[.,]\d+)?)m?)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Feststellungen aus Freitext laden ────────────────────────────────

    /// <summary>
    /// Parst VSA-Schadenscodes aus dem Freitext-Feld "Primaere_Schaeden".
    /// Nur Codes, die in knownCodes enthalten sind, werden beruecksichtigt.
    /// </summary>
    internal static List<VsaFinding> ParseFindingsFromPrimaryDamage(string? raw, IReadOnlySet<string> knownCodes)
    {
        var findings = new List<VsaFinding>();
        if (string.IsNullOrWhiteSpace(raw))
            return findings;

        var lines = raw.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var match = LeadingTokenRegex.Match(line);
            if (!match.Success)
                continue;

            var code = NormalizeCode(match.Value);
            if (code.Length < 3)
                continue;

            var baseCode = code.Length >= 3 ? code[..3] : code;
            if (knownCodes.Count > 0 && !knownCodes.Contains(code) && !knownCodes.Contains(baseCode))
                continue;

            findings.Add(new VsaFinding
            {
                KanalSchadencode = code,
                Raw = line
            });
        }

        return findings;
    }

    // ── Bestehende Feststellungen anreichern ─────────────────────────────

    /// <summary>
    /// Reichert bereits geparste Feststellungen mit Daten aus dem Primaerschaden-Freitext an.
    /// Ergaenzt Basiscodes (z.B. "BAJ") um Untercodes (z.B. "BAJC") und extrahiert Quantifizierungswerte.
    /// </summary>
    internal static IEnumerable<VsaFinding> EnrichFindingsFromPrimaryDamage(
        IEnumerable<VsaFinding> findings,
        string? primaryDamageText)
    {
        var candidates = ParsePrimaryDamageCodeCandidates(primaryDamageText);
        if (candidates.Count == 0)
            return findings;

        return findings.Select(finding =>
        {
            var code = NormalizeCode(finding.KanalSchadencode);
            var effectiveCode = code;
            if (code.Length == 3)
            {
                var meter = finding.MeterStart ?? finding.SchadenlageAnfang;
                var candidate = FindMatchingFullCodeCandidate(candidates, code, meter);
                if (candidate is not null)
                    effectiveCode = candidate.Code;
            }

            var q1 = string.IsNullOrWhiteSpace(finding.Quantifizierung1)
                ? ExtractQuantValue(finding.Raw)
                : finding.Quantifizierung1;

            return string.Equals(effectiveCode, code, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(q1, finding.Quantifizierung1, StringComparison.Ordinal)
                ? finding
                : CopyFinding(finding, effectiveCode, q1);
        });
    }

    // ── Interne Hilfsmethoden ─────────────────────────────────────────────

    private static PrimaryDamageCodeCandidate? FindMatchingFullCodeCandidate(
        IReadOnlyList<PrimaryDamageCodeCandidate> candidates,
        string baseCode,
        double? meter)
    {
        var matchingBase = candidates
            .Where(c => c.Code.Length > baseCode.Length
                        && c.Code.StartsWith(baseCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingBase.Count == 0)
            return null;

        if (meter.HasValue)
        {
            return matchingBase
                .Where(c => c.Meter.HasValue)
                .OrderBy(c => Math.Abs(c.Meter!.Value - meter.Value))
                .FirstOrDefault(c => Math.Abs(c.Meter!.Value - meter.Value) <= 0.05);
        }

        return matchingBase.Count == 1 ? matchingBase[0] : null;
    }

    private static List<PrimaryDamageCodeCandidate> ParsePrimaryDamageCodeCandidates(string? raw)
    {
        var candidates = new List<PrimaryDamageCodeCandidate>();
        if (string.IsNullOrWhiteSpace(raw))
            return candidates;

        var lines = raw.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var match = PrimaryDamageLineRegex.Match(rawLine.Trim());
            if (!match.Success)
                continue;

            var code = NormalizeCode(match.Groups["code"].Value);
            if (code.Length <= 3)
                continue;

            double? meter = null;
            var meterText = match.Groups["meter"].Value;
            if (!string.IsNullOrWhiteSpace(meterText)
                && double.TryParse(meterText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var meterValue))
            {
                meter = meterValue;
            }

            candidates.Add(new PrimaryDamageCodeCandidate(code, meter));
        }

        return candidates;
    }

    // ── Public-Hilfsmethoden (werden auch von VsaEvaluationService referenziert) ──

    /// <summary>
    /// Extrahiert einen Quantifizierungswert (Prozent, Grad oder Millimeter) aus einem Rohtext.
    /// Gibt null zurueck wenn kein Wert gefunden wurde.
    /// </summary>
    internal static string? ExtractQuantValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var percent = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*%");
        if (percent.Success)
            return NormalizeQuant(percent.Groups[1].Value);

        var degrees = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*(?:°|deg\b|Grad\b|degrees\b)", RegexOptions.IgnoreCase);
        if (degrees.Success)
            return NormalizeQuant(degrees.Groups[1].Value);

        var millimeters = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*mm\b", RegexOptions.IgnoreCase);
        if (millimeters.Success)
            return NormalizeQuant(millimeters.Groups[1].Value);

        return null;
    }

    private static string NormalizeQuant(string value)
        => value.Replace(',', '.');

    /// <summary>
    /// Kopiert eine Feststellung und ueberschreibt Code und Quantifizierung1.
    /// </summary>
    internal static VsaFinding CopyFinding(VsaFinding source, string code, string? quantification1)
        => new()
        {
            KanalSchadencode = code,
            Quantifizierung1 = quantification1,
            Quantifizierung2 = source.Quantifizierung2,
            SchadenlageAnfang = source.SchadenlageAnfang,
            SchadenlageEnde = source.SchadenlageEnde,
            LL = source.LL,
            Raw = source.Raw,
            MeterStart = source.MeterStart,
            MeterEnd = source.MeterEnd,
            MPEG = source.MPEG,
            Timestamp = source.Timestamp,
            FotoPath = source.FotoPath,
            EZD = source.EZD,
            EZS = source.EZS,
            EZB = source.EZB
        };

    /// <summary>
    /// Normalisiert einen VSA-Schadencode: Whitespace, Sonderzeichen und Unterklassen entfernen,
    /// Grossbuchstaben. Beispiel: "BAJ.C @0.10m" → "BAJC".
    /// </summary>
    internal static string NormalizeCode(string? raw)
        => VsaConditionScorer.NormalizeCode(raw);
}

/// <summary>Kandidat aus dem Primaerschaden-Freitext: Code + optionaler Meterstand.</summary>
internal sealed record PrimaryDamageCodeCandidate(
    string Code,
    double? Meter);
