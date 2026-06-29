using System.Globalization;
using System.Text;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>
/// Reine Hilfsklasse fuer Zahl- und Header-Normalisierung im Excel-Export.
/// Kein ClosedXML-Bezug – ausschliesslich string/double-Logik.
/// </summary>
public static class ExcelCellFormatting
{
    /// <summary>
    /// Versucht einen Rohwert als Dezimalzahl zu parsen.
    /// Unterstuetzt CH-Dezimaltrenner (Apostroph als Tausender), DE-Format (Punkt als Tausender,
    /// Komma als Dezimal) sowie Invariant-Format.
    /// </summary>
    public static bool TryParseExcelNumber(string? value, out double result)
    {
        result = 0;
        var s = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(s))
            return false;

        // CH-Apostroph und Leerzeichen (inkl. geschuetztes) als Tausendertrenner entfernen
        s = s.Replace("'", "").Replace(" ", "").Replace(" ", "");

        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        var decimalSeparator = lastComma >= 0 && lastDot >= 0
            ? (lastComma > lastDot ? ',' : '.')
            : (lastComma >= 0 ? ',' : '.');

        var normalized = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (char.IsDigit(ch) || (i == 0 && ch is '+' or '-'))
            {
                normalized.Append(ch);
                continue;
            }

            if (ch == decimalSeparator)
            {
                normalized.Append('.');
                continue;
            }

            // Restliche Punkt/Komma als Tausendertrenner ignorieren
            if (ch is '.' or ',')
                continue;

            return false;
        }

        return double.TryParse(normalized.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Normalisiert einen Spalten-Header fuer den Abgleich:
    /// Umlaute werden aufgeloest, Mojibake-Sequenzen (UTF-8-in-Latin1) werden korrigiert,
    /// Leerzeichen am Rand werden entfernt, Ergebnis ist Kleinbuchstaben.
    /// </summary>
    public static string NormalizeHeader(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var s = text.Trim();

        // Echte Unicode-Umlaute
        s = s.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        s = s.Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue");

        // Mojibake (UTF-8-Umlaut-Bytes falsch als Latin-1 gelesen)
        s = s.Replace("Ã¤", "ae").Replace("Ã¶", "oe").Replace("Ã¼", "ue").Replace("ÃŸ", "ss");
        s = s.Replace("Ã„", "Ae").Replace("Ã–", "Oe").Replace("Ãœ", "Ue");
        s = s.Replace("Â", "");

        return s.ToLowerInvariant();
    }
}
