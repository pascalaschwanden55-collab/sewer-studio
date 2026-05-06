// AuswertungPro – Video-Selbsttraining Phase 1
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Application.Ai.Training.Services;

namespace AuswertungPro.Next.Application.Ai.Training.Services;

/// <summary>
/// Konvertiert importierte Protokolleintraege (WinCan, IBAK, etc.)
/// in normalisierte GroundTruthEntry-Objekte fuer den Video-Vergleich.
/// </summary>
public static class ProtocolToGroundTruthMapper
{
    /// <summary>
    /// Mappt alle ProtocolEntries eines Dokuments auf GroundTruthEntries.
    /// Eintraege ohne Code oder ohne MeterStart werden uebersprungen.
    /// </summary>
    public static List<GroundTruthEntry> Map(
        ProtocolDocument document,
        string? rohrmaterial = null,
        int? nennweiteMm = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var entries = document.Original.Entries;
        if (entries is null || entries.Count == 0)
            return [];

        var result = new List<GroundTruthEntry>(entries.Count);

        foreach (var pe in entries)
        {
            if (pe.IsDeleted)
                continue;

            // Eintraege ohne VSA-Code ueberspringen (z.B. KINS-Import)
            if (string.IsNullOrWhiteSpace(pe.Code))
                continue;

            // Eintraege ohne Meterposition ueberspringen
            if (!pe.MeterStart.HasValue)
                continue;

            var meterStart = pe.MeterStart.Value;
            var meterEnd = pe.MeterEnd ?? meterStart;

            var gt = new GroundTruthEntry
            {
                MeterStart = meterStart,
                MeterEnd = meterEnd,
                VsaCode = pe.Code.Trim().ToUpperInvariant(),
                Text = pe.Beschreibung ?? "",
                IsStreckenschaden = pe.IsStreckenschaden,
                Zeit = pe.Zeit,
                Quantification = ExtractQuantification(pe),
                Characterization = ExtractCharacterization(pe),
                ClockPosition = ExtractClockPosition(pe),
                ConnectionClock = ExtractConnectionClock(pe),
                Rohrmaterial = rohrmaterial,
                NennweiteMm = nennweiteMm,
                Freitext = pe.Beschreibung,
                ExtractedFramePath = pe.FotoPaths.Count > 0 ? pe.FotoPaths[0] : null
            };

            result.Add(gt);
        }

        return result;
    }

    /// <summary>
    /// Extrahiert die primaere Uhrzeigerposition aus CodeMeta.Parameters.
    /// Schluessel: "ClockPos1" (WinCan), "Uhrlage" (generisch).
    /// </summary>
    private static string? ExtractClockPosition(ProtocolEntry pe)
    {
        var p = pe.CodeMeta?.Parameters;
        if (p is null || p.Count == 0) return null;

        if (p.TryGetValue("ClockPos1", out var cp1) && !string.IsNullOrWhiteSpace(cp1))
            return NormalizeClock(cp1);

        if (p.TryGetValue("Uhrlage", out var uhr) && !string.IsNullOrWhiteSpace(uhr))
            return NormalizeClock(uhr);

        return null;
    }

    /// <summary>
    /// Extrahiert die Anschluss-Uhrzeigerposition (ClockPos2 aus WinCan).
    /// </summary>
    private static string? ExtractConnectionClock(ProtocolEntry pe)
    {
        var p = pe.CodeMeta?.Parameters;
        if (p is null || p.Count == 0) return null;

        if (p.TryGetValue("ClockPos2", out var cp2) && !string.IsNullOrWhiteSpace(cp2))
            return NormalizeClock(cp2);

        return null;
    }

    /// <summary>
    /// Normalisiert Uhrlage-Angaben: "3:00" → "3", "9 Uhr" → "9", "12" → "12".
    /// </summary>
    private static string? NormalizeClock(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var cleaned = raw
            .Replace("Uhr", "", StringComparison.OrdinalIgnoreCase)
            .Replace(":00", "")
            .Replace("o'clock", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        // Versuche Ganzzahl zu parsen (1-12)
        if (int.TryParse(cleaned, out var h) && h >= 1 && h <= 12)
            return h.ToString();

        // Dezimalwert wie "3.5" akzeptieren
        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return cleaned;

        return null;
    }

    /// <summary>
    /// Extrahiert Characterization aus Char1/Char2 (WinCan-Format).
    /// Ergibt z.B. "B.A" fuer Riss quer, laengs.
    /// </summary>
    private static string? ExtractCharacterization(ProtocolEntry pe)
    {
        var p = pe.CodeMeta?.Parameters;
        if (p is null || p.Count == 0) return null;

        p.TryGetValue("Char1", out var c1);
        p.TryGetValue("Char2", out var c2);

        var hasC1 = !string.IsNullOrWhiteSpace(c1);
        var hasC2 = !string.IsNullOrWhiteSpace(c2);

        if (!hasC1 && !hasC2) return null;
        if (hasC1 && hasC2) return $"{c1!.Trim()}.{c2!.Trim()}";
        return hasC1 ? c1!.Trim() : c2!.Trim();
    }

    /// <summary>
    /// Extrahiert eine strukturierte Quantifizierung aus Q1/Q2/Q3.
    /// Q1 ist der Hauptwert (z.B. Prozent Querschnitt oder mm Breite).
    /// </summary>
    private static QuantificationDetail? ExtractQuantification(ProtocolEntry pe)
    {
        var p = pe.CodeMeta?.Parameters;
        if (p is null || p.Count == 0) return null;

        if (!p.TryGetValue("Q1", out var q1) || string.IsNullOrWhiteSpace(q1))
            return null;

        // Q1 parsen — kann "30", "30%", "3.5 mm" etc. sein
        var (value, unit) = ParseQuantValue(q1);
        if (value is null)
            return null;

        // Typ aus Code ableiten (vereinfacht)
        var type = InferQuantificationType(pe.Code, unit);

        return new QuantificationDetail
        {
            Value = value.Value,
            Unit = unit ?? "%",
            Type = type
        };
    }

    /// <summary>
    /// Parst einen Quantifizierungswert wie "30", "30%", "3.5 mm".
    /// </summary>
    private static (double? Value, string? Unit) ParseQuantValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (null, null);

        raw = raw.Trim();

        // Einheit abtrennen
        string? unit = null;
        var numPart = raw;

        if (raw.EndsWith('%'))
        {
            unit = "%";
            numPart = raw[..^1].Trim();
        }
        else if (raw.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
        {
            unit = "mm";
            numPart = raw[..^2].Trim();
        }
        else if (raw.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
        {
            unit = "cm";
            numPart = raw[..^2].Trim();
        }

        // Komma zu Punkt normalisieren
        numPart = numPart.Replace(',', '.');

        if (double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return (val, unit);

        return (null, null);
    }

    /// <summary>
    /// Leitet den Quantifizierungstyp aus dem VSA-Code ab.
    /// </summary>
    private static string InferQuantificationType(string code, string? unit)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "Unbekannt";

        // Prefix-basierte Zuordnung (VSA-KEK)
        var prefix = code.Length >= 3 ? code[..3].ToUpperInvariant() : code.ToUpperInvariant();

        return prefix switch
        {
            "BAA" => "Deformation",          // % Deformation
            "BAB" => "Rissbreite",           // mm Breite
            "BAC" => "Bruchlaenge",          // mm Laenge
            "BAE" => "Moerteltiefe",         // mm Tiefe
            "BAG" => "Dichtungslaenge",      // mm Laenge
            "BAH" => "Versatz",              // mm Abstand
            "BAI" => "Einragung",            // mm Einragung
            "BBA" => "Querschnittsverminderung", // % Querschnitt
            "BBB" => "Querschnittsverminderung", // % Querschnitt
            "BBC" => "Querschnittsverminderung", // % Querschnitt
            "BBD" => "Querschnittsverminderung", // % Querschnitt
            "BBE" => "Querschnittsverminderung", // % Querschnitt
            "BBH" => "Anzahl",               // Stueck
            "BCA" => "Anschlussgroesse",     // mm Hoehe/Breite
            "BCC" => "Bogenwinkel",          // Grad
            "BDD" => "Wasserstand",          // % lichte Hoehe
            _ => unit switch
            {
                "%" => "Prozent",
                "mm" => "Laenge",
                _ => "Unbekannt"
            }
        };
    }
}
