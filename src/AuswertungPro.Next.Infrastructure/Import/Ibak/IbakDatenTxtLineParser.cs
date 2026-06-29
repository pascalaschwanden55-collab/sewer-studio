using System;
using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Rein statische Zeilen-Parsing-Logik für IBAK Daten.txt.
/// Kein Datei-IO und kein Datenbankzugriff — alle Methoden
/// arbeiten ausschließlich auf Strings und einfachen Werttypen.
/// </summary>
internal static class IbakDatenTxtLineParser
{
    // Regex-Konstanten analog zu IbakExportImportService (verbatim übernommen)
    internal static readonly Regex ObservationRegex = new(
        @"^\s*(\d{2}:\d{2}:\d{2})\s+([\d.,]+)\s*m\s+([A-Z0-9]+)\s+(.*)$",
        RegexOptions.Compiled);

    internal static readonly Regex HeaderLineRegex = new(
        @"^\s+([\d.,]+)\s*m\s+([A-Z0-9]+)\s+(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex RangeIndexRegex = new(@"\((\d+)\)", RegexOptions.Compiled);

    /// <summary>
    /// Entfernt das IBAK-interne Meta-Trennzeichen und gibt den bereinigten Text zurück.
    /// </summary>
    internal static string StripIbakMeta(string text)
    {
        var idx = text.IndexOf("@!$ibak$!", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return text[..idx].Trim();
        return text.Trim();
    }

    /// <summary>
    /// Parst einen Meterstand-Text (Komma oder Punkt als Dezimaltrenner).
    /// Gibt null zurück wenn der Text leer oder nicht parsebar ist.
    /// </summary>
    internal static double? ParseMeter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var normalized = text.Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;
        return null;
    }

    /// <summary>
    /// Parst eine Zeitangabe im Format HH:MM:SS.
    /// Gibt null zurück wenn der Text leer oder nicht parsebar ist.
    /// </summary>
    internal static TimeSpan? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var ts))
            return ts;
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out ts))
            return ts;
        return null;
    }

    /// <summary>
    /// Erkennt ob eine Beschreibung einen Strecken-Start (Anfang/Beginn) oder
    /// Strecken-End (Ende) bezeichnet und extrahiert ggf. einen numerischen Index
    /// aus runden Klammern.
    /// </summary>
    internal static (bool isStart, bool isEnd, string index) ExtractRange(string desc)
    {
        var lower = desc.ToLowerInvariant();
        var isStart = lower.Contains("anfang") || lower.Contains("beginn");
        var isEnd = lower.Contains("ende");
        var index = "0";
        var m = RangeIndexRegex.Match(desc);
        if (m.Success)
            index = m.Groups[1].Value;
        return (isStart, isEnd, index);
    }

    /// <summary>
    /// Erzeugt einen neuen <see cref="ProtocolEntry"/> aus den geparsten Zeilenfeldern.
    /// </summary>
    internal static ProtocolEntry BuildEntry(string code, string desc, double? meter, string? mpeg, TimeSpan? time)
    {
        return new ProtocolEntry
        {
            Code = code,
            Beschreibung = desc,
            MeterStart = meter,
            MeterEnd = meter,
            Mpeg = mpeg,
            Zeit = time,
            Source = ProtocolEntrySource.Imported
        };
    }

    /// <summary>
    /// Mappt IBAK-Materialnamen auf normalisierte Kurzzeichen.
    /// Unbekannte Werte werden unverändert zurückgegeben.
    /// </summary>
    internal static string MapMaterial(string ibakMaterial)
    {
        var lower = ibakMaterial.ToLowerInvariant();
        if (lower.Contains("polypropylen")) return "PP";
        if (lower.Contains("polyvinylchlorid") || lower.Contains("pvc")) return "PVC";
        if (lower.Contains("polyethylen") || lower.Contains("pe")) return "PE";
        if (lower.Contains("beton") || lower.Contains("normalbeton")) return "Beton";
        if (lower.Contains("steinzeug")) return "Steinzeug";
        if (lower.Contains("guss")) return "Guss";
        if (lower.Contains("gfk") || lower.Contains("glasfaser")) return "GFK";
        return ibakMaterial; // Originalwert beibehalten
    }
}
