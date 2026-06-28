using System;
using System.Collections.Generic;
using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Reine Transformation: wandelt eine Folge von Protokoll-Eintraegen in eine
/// deduplizierte Liste von VsaFinding-Objekten um (IO-frei, keine Seiteneffekte).
/// </summary>
internal static class WinCanFindingFactory
{
    /// <summary>
    /// Erstellt deduplizierte VsaFinding-Objekte aus den gegebenen Protokoll-Eintraegen.
    /// Dedup-Schluessel: effektiver VSA-Code + Meterstand (Code|Meter).
    /// Q1-Extraktion erfolgt fuer Codes BAA, BAB, BAC, BAF, BBA, BDD.
    /// </summary>
    /// <param name="entries">Protokoll-Eintraege in der gewuenschten Reihenfolge.</param>
    /// <returns>Neue, deduplizierte Liste von VsaFinding-Objekten.</returns>
    public static List<VsaFinding> BuildFindings(IReadOnlyList<ProtocolEntry> entries)
    {
        var findings = new List<VsaFinding>(entries.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var rawCode = (entry.Code ?? "").Trim().ToUpperInvariant();

            // Streckenschaden-Marker zum echten VSA-Code aufloesen
            var effectiveCode = ContinuousDefectCodeResolver.ResolveEffectiveCode(
                rawCode, entry.Beschreibung, out _);

            var meterStart = entry.MeterStart;
            var meterEnd = entry.MeterEnd;
            var meterKey = meterStart.HasValue
                ? meterStart.Value.ToString("F2", CultureInfo.InvariantCulture)
                : meterEnd.HasValue
                    ? meterEnd.Value.ToString("F2", CultureInfo.InvariantCulture)
                    : string.Empty;

            var dedupeKey = $"{effectiveCode}|{meterKey}";
            if (!seen.Add(dedupeKey))
                continue;

            var finding = new VsaFinding
            {
                KanalSchadencode = effectiveCode,
                Raw = entry.Beschreibung,
                MeterStart = meterStart,
                MeterEnd = meterEnd,
                SchadenlageAnfang = meterStart,
                SchadenlageEnde = meterEnd,
                MPEG = entry.Mpeg,
                FotoPath = entry.FotoPaths.Count > 0 ? entry.FotoPaths[0] : null
            };

            // Q1-Extraktion aus Beschreibung (WinCan liefert kein Q1-Feld).
            // Nur fuer Codes mit QuantRules: BAA, BAB, BAC, BAF, BBA, BDD.
            if (string.IsNullOrEmpty(finding.Quantifizierung1) && !string.IsNullOrEmpty(entry.Beschreibung))
            {
                var normCode = effectiveCode.Length >= 3
                    ? effectiveCode[..3].ToUpperInvariant()
                    : "";
                if (normCode is "BAA" or "BAB" or "BAC" or "BAF" or "BBA" or "BDD")
                {
                    finding.Quantifizierung1 = ExtractQuantValue(entry.Beschreibung);
                }
            }

            findings.Add(finding);
        }

        return findings;
    }

    /// <summary>
    /// Delegiert die Q1-Extraktion an <see cref="WinCanValueNormalizer"/>.
    /// </summary>
    private static string? ExtractQuantValue(string beschreibung)
        => WinCanValueNormalizer.ExtractQuantValue(beschreibung);
}
