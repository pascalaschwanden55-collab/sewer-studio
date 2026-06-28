using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Reine Normalisierungs-Hilfsmethoden fuer den XTF/SIA405-Import.
/// Alle Methoden sind zustandslos und haben keine IO- oder Dokument-Abhaengigkeiten.
/// Extrahiert aus LegacyXtfImportService.
/// </summary>
internal static class XtfValueNormalizer
{
    /// <summary>
    /// Normalisiert SIA405-Materialbezeichnungen auf lesbare deutsche Texte.
    /// </summary>
    public static string NormalizeSiaMaterial(string material)
    {
        material ??= "";
        if (string.IsNullOrWhiteSpace(material)) return "";

        if (Regex.IsMatch(material, "Kunststoff_Hartpolyethylen", RegexOptions.IgnoreCase)) return "Kunststoff PE-HD";
        if (Regex.IsMatch(material, "Kunststoff_Polyethylen", RegexOptions.IgnoreCase)) return "Kunststoff PE";
        if (Regex.IsMatch(material, "Kunststoff_Polyvinylchlorid", RegexOptions.IgnoreCase)) return "Kunststoff PVC";
        if (Regex.IsMatch(material, "Beton_Normalbeton", RegexOptions.IgnoreCase)) return "Beton";
        if (Regex.IsMatch(material, "Beton_", RegexOptions.IgnoreCase)) return "Beton";
        if (Regex.IsMatch(material, "Steinzeug", RegexOptions.IgnoreCase)) return "Steinzeug";

        material = material.Replace("_", " ").Trim();
        if (material.Length == 0) return "";
        return char.ToUpperInvariant(material[0]) + material[1..];
    }

    /// <summary>
    /// Normalisiert den SIA405-Nutzungsart-Wert auf den deutschen Bezeichner.
    /// </summary>
    public static string NormalizeNutzungsart(string v)
    {
        v ??= "";
        if (Regex.IsMatch(v, "(?i)Schmutzabwasser")) return "Schmutzwasser";
        if (Regex.IsMatch(v, "(?i)Regenabwasser")) return "Regenwasser";
        if (Regex.IsMatch(v, "(?i)Mischabwasser")) return "Mischabwasser";
        return v.Trim();
    }

    /// <summary>
    /// Wandelt ein Datum im Format yyyymmdd in dd.MM.yyyy um.
    /// Unbekannte Formate werden unveraendert zurueckgegeben (getrimmt).
    /// </summary>
    public static string NormalizeDate_yyyymmdd(string? yyyymmdd)
    {
        yyyymmdd ??= "";
        var m = Regex.Match(yyyymmdd.Trim(), @"^(\d{4})(\d{2})(\d{2})$");
        if (!m.Success) return yyyymmdd.Trim();
        return $"{m.Groups[3].Value}.{m.Groups[2].Value}.{m.Groups[1].Value}";
    }

    /// <summary>
    /// Parst einen Double-Wert aus einem String; unterstuetzt Komma und Punkt als Dezimaltrennzeichen.
    /// Faellt bei einfachem Parse-Fehler auf einen Regex-Extrakt-Versuch zurueck.
    /// </summary>
    public static bool TryParseDouble(string? s, out double value)
    {
        value = 0.0;
        if (string.IsNullOrWhiteSpace(s))
            return false;
        s = s.Trim().Replace(",", ".");
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return true;

        var match = Regex.Match(s, @"-?\d+(?:[.,]\d+)?");
        if (!match.Success)
            return false;

        var number = match.Value.Replace(",", ".");
        return double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Normalisiert einen VSA/SIA-Schadencode: Whitespace trimmen, Grossbuchstaben, Sonderzeichen entfernen.
    /// </summary>
    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;
        return Regex.Replace(code.Trim().ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
    }

    /// <summary>
    /// Berechnet einen Aehnlichkeitsrang fuer zwei normalisierten Codes.
    /// 0 = exakt gleich, 1 = Praefix-Match, 2 = unterschiedlich.
    /// </summary>
    public static int GetCodeSimilarityRank(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return 2;
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (left.StartsWith(right, StringComparison.OrdinalIgnoreCase)
            || right.StartsWith(left, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    /// <summary>
    /// Parst eine MPEG-Zeitangabe (z.B. "01:23:45" oder "23:45") in einen TimeSpan.
    /// Gibt null zurueck wenn das Format nicht erkannt wird.
    /// </summary>
    public static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed) ? parsed : null;
    }
}
