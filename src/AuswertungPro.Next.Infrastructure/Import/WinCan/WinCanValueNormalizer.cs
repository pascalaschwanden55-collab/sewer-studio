using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Reine Wert-Normalisierer fuer WinCan-Importe.
/// Kein IO, kein SQLite, kein Threading – nur Wert-in/Wert-raus.
/// </summary>
internal static class WinCanValueNormalizer
{
    /// <summary>
    /// Wandelt einen rohen Zahlenwert aus WinCan in eine kanonische Darstellung um.
    /// Ganzzahlige Werte werden ohne Dezimaltrenner zurueckgegeben.
    /// </summary>
    public static string? NormalizeNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var canonical = text.Contains(',', StringComparison.Ordinal) && !text.Contains('.', StringComparison.Ordinal)
            ? text.Replace(',', '.')
            : text;
        if (double.TryParse(canonical, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
        {
            if (Math.Abs(val - Math.Round(val)) < 0.01)
                return ((int)Math.Round(val)).ToString(CultureInfo.InvariantCulture);
            return val.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return text;
    }

    /// <summary>
    /// Liefert Datumstext aus WinCan-Feldern.
    /// Vorzug hat der Klartext (yearText); Fallback ist ParseSqliteDate auf rawDate.
    /// </summary>
    public static string? NormalizeDate(string? yearText, string? rawDate)
    {
        if (!string.IsNullOrWhiteSpace(yearText))
            return yearText.Trim();

        var dt = ParseSqliteDate(rawDate);
        if (dt.HasValue)
            return dt.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return null;
    }

    /// <summary>
    /// Mappt WinCan-Nutzungsarten-Kurzformen auf deutschsprachige Klartext-Werte.
    /// Ungueltige oder nicht zuzuordnende Werte werden als null zurueckgegeben.
    /// </summary>
    public static string? NormalizeUsage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        var lower = t.ToLowerInvariant();

        // Nicht-Nutzungsarten-Werte filtern
        if (lower is "gereinigt" or "nicht gereinigt" or "verschmutzt"
            or "ja" or "nein" or "yes" or "no"
            or "-" or "--" or "n/a" or "k.a.")
            return null;

        // Volltext-Pruefungen
        if (lower.Contains("regen"))
            return "Regenwasser";
        if (lower.Contains("schmutz"))
            return "Schmutzwasser";
        if (lower.Contains("misch"))
            return "Mischabwasser";

        // DWA-M150 / ISYBAU / VSA Kurzformen
        if (lower is "s" or "ks" or "sw") return "Schmutzwasser";
        if (lower is "r" or "kr" or "rw") return "Regenwasser";
        if (lower is "m" or "km" or "mw") return "Mischabwasser";

        // Unbekannte Kurzformen (Schweizer VSA: E, H, F, Z usw.) werden uebersprungen.
        if (t.Length <= 2)
            return null;

        return t;
    }

    /// <summary>
    /// Wandelt WinCan-Inspektionsrichtungs-Codes (1/2) in Klartext um.
    /// </summary>
    public static string? NormalizeInspectionDir(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        if (t == "1")
            return "In Fliessrichtung";
        if (t == "2")
            return "Gegen Fliessrichtung";

        return t;
    }

    /// <summary>
    /// Wandelt WinCan-Zugaenglichkeits-Flags (0/1/true/false/ja/nein) in Klartext um.
    /// </summary>
    public static string? NormalizeAccessible(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim().ToLowerInvariant();
        if (t is "1" or "true" or "ja" or "yes")
            return "offen";
        if (t is "0" or "false" or "nein" or "no")
            return "abgeschlossen";

        return raw.Trim();
    }

    /// <summary>
    /// Parst einen SQLite-Datumswert aus WinCan.
    /// Unterstuetzt Unix-Millisekunden ("Date(...)"), europaeische und ISO-Formate.
    /// </summary>
    public static DateTime? ParseSqliteDate(object? raw)
    {
        if (raw is null)
            return null;
        var text = raw.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var m = Regex.Match(text, @"Date\((\d+)\)");
        if (m.Success && long.TryParse(m.Groups[1].Value, out var ms))
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).DateTime;

        // Europaeische Formate zuerst, um DD/MM-Verwechslung zu vermeiden
        var formats = new[] { "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "dd.MM.yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtExact))
            return dtExact;

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            return dt;

        return null;
    }

    /// <summary>
    /// Extrahiert einen numerischen Quantifizierungswert aus WinCan-Beschreibungstexten.
    /// Sucht nach Prozent (%), Grad (deg) oder Millimeter (mm) Angaben.
    /// </summary>
    public static string? ExtractQuantValue(string beschreibung)
    {
        // Prozent: "5%", "25 %", "10.5%"
        var m = Regex.Match(beschreibung, @"(\d+(?:[.,]\d+)?)\s*%");
        if (m.Success) return m.Groups[1].Value.Replace(',', '.');

        // Grad: "15°", "45 °"
        m = Regex.Match(beschreibung, @"(\d+(?:[.,]\d+)?)°");
        if (m.Success) return m.Groups[1].Value.Replace(',', '.');

        // Millimeter: "2mm", "0.5 mm"
        m = Regex.Match(beschreibung, @"(\d+(?:[.,]\d+)?)\s*mm");
        if (m.Success) return m.Groups[1].Value.Replace(',', '.');

        return null;
    }

    /// <summary>
    /// Parst einen Zeitstempel-String (Timecode) aus WinCan in ein TimeSpan.
    /// Unterstuetzt hh:mm:ss, mm:ss und Varianten mit Millisekunden.
    /// </summary>
    public static TimeSpan? ParseTimeSpan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"hh\:mm\:ss\.ff", @"hh\:mm\:ss\.fff", @"mm\:ss\.ff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;
        return null;
    }

    /// <summary>
    /// Prueft ob der WinCan-Medientyp ein Bild ist.
    /// </summary>
    public static bool IsImage(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return false;
        var t = type.Trim().ToUpperInvariant();
        return t is "JPG" or "JPEG" or "PNG" or "BMP";
    }

    /// <summary>
    /// Prueft ob der WinCan-Medientyp ein Video ist.
    /// </summary>
    public static bool IsVideo(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return false;
        var t = type.Trim().ToUpperInvariant();
        return t is "MPG" or "MPEG" or "MP4" or "AVI" or "MOV";
    }
}
