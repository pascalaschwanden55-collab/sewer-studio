using System;
using System.Globalization;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Reine Parsing-Hilfsklasse fuer DN-Werte (Nennweite in mm).
/// Unterstuetzt Komma- und Punkt-Dezimaltrenner, Tausender-Trennzeichen
/// sowie gemischte Formate. Aus <c>DataPageViewModel.TryParseDnMm</c> extrahiert
/// (verhaltensneutral).
/// </summary>
public static class DnValueParser
{
    /// <summary>
    /// Versucht, einen DN-Rohwert (Nennweite in mm) zu parsen.
    /// Gibt <c>null</c> zurueck, wenn der Wert leer oder nicht auflösbar ist,
    /// oder wenn der geparste Wert &lt;= 0 ist.
    /// </summary>
    public static double? TryParseMillimeters(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);

        // Direktversuch: InvariantCulture und CurrentCulture
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            && value > 0)
        {
            return value;
        }

        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value)
            && value > 0)
        {
            return value;
        }

        // Gemischtes Format: Punkt UND Komma vorhanden
        if (text.Contains(',') && text.Contains('.'))
        {
            var commaAsDecimal = text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
            if (double.TryParse(commaAsDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;

            var dotAsDecimal = text.Replace(",", string.Empty, StringComparison.Ordinal);
            if (double.TryParse(dotAsDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;
        }
        else if (text.Contains(','))
        {
            // Komma als Dezimaltrenner
            var normalized = text.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;
        }

        // Letzte Chance: alle Trennzeichen entfernen, Mindestwert 50 (plausible DN)
        var digitsOnly = text.Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        if (double.TryParse(digitsOnly, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 50)
            return value;

        return null;
    }
}
