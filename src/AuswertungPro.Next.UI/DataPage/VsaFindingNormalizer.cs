using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Normalisiert die VsaFindings einer Haltung: ergaenzt fehlende Meter-/Zeitangaben
/// aus dem Roh-Text und dedupliziert gleiche Befunde. Reine Logik, mutiert nur den
/// Record. Der UI-State (Dirty, Grid-Refresh, Autosave) bleibt im ViewModel.
/// </summary>
public static class VsaFindingNormalizer
{
    private static readonly Regex MeterRegex = new(@"@?\s*(\d+(?:[.,]\d+)?)\s*m(?!m)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeRegex = new(@"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", RegexOptions.Compiled);

    /// <summary>
    /// Ergaenzt Meter/Zeit aus dem Roh-Text und dedupliziert die Findings in place.
    /// Liefert true, wenn sich etwas geaendert hat (dann muss das ViewModel speichern/refreshen).
    /// </summary>
    public static bool Normalize(HaltungRecord record)
    {
        if (record.VsaFindings is null || record.VsaFindings.Count == 0)
            return false;

        var changed = false;
        foreach (var f in record.VsaFindings)
        {
            var raw = f.Raw ?? string.Empty;

            if (f.MeterStart is null && f.SchadenlageAnfang is null && !string.IsNullOrWhiteSpace(raw))
            {
                var meter = TryParseMeter(raw);
                if (meter is not null)
                {
                    f.MeterStart = meter;
                    changed = true;
                }
            }

            if (f.MeterEnd is null && f.SchadenlageEnde is null && !string.IsNullOrWhiteSpace(raw))
            {
                // If no explicit end, leave empty; but if text has a second meter, use it.
                var meterRange = TryParseSecondMeter(raw);
                if (meterRange is not null)
                {
                    f.MeterEnd = meterRange;
                    changed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(f.MPEG) && !string.IsNullOrWhiteSpace(raw))
            {
                var mpeg = TryParseTime(raw);
                if (!string.IsNullOrWhiteSpace(mpeg))
                {
                    f.MPEG = mpeg;
                    changed = true;
                }
            }
        }

        var deduped = new List<VsaFinding>(record.VsaFindings.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var finding in record.VsaFindings)
        {
            var effectiveCode = DataPageProtocolObservationMapper.ResolveFindingEffectiveCode(finding.KanalSchadencode, finding.Raw);
            if (!string.Equals(finding.KanalSchadencode, effectiveCode, StringComparison.OrdinalIgnoreCase))
            {
                finding.KanalSchadencode = effectiveCode;
                changed = true;
            }

            var meter = finding.MeterStart ?? finding.SchadenlageAnfang;
            var meterKey = meter.HasValue
                ? meter.Value.ToString("F2", CultureInfo.InvariantCulture)
                : string.Empty;
            var key = $"{effectiveCode}|{meterKey}";
            if (!seen.Add(key))
            {
                changed = true;
                continue;
            }

            deduped.Add(finding);
        }

        if (deduped.Count != record.VsaFindings.Count)
        {
            record.VsaFindings = deduped;
            changed = true;
        }

        return changed;
    }

    private static double? TryParseMeter(string raw)
    {
        var match = MeterRegex.Match(raw);
        if (!match.Success)
            return null;

        var valueText = match.Groups[1].Value.Replace(',', '.');
        if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static double? TryParseSecondMeter(string raw)
    {
        var matches = MeterRegex.Matches(raw);
        if (matches.Count < 2)
            return null;

        var valueText = matches[1].Groups[1].Value.Replace(',', '.');
        if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static string? TryParseTime(string raw)
    {
        var match = TimeRegex.Match(raw);
        if (!match.Success)
            return null;

        return match.Groups[1].Value;
    }
}
