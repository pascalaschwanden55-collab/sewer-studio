using System;
using System.Globalization;
using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Statische Hilfsklasse fuer reine Eingabe-Validierung im VSA-Code-Erfassungsdialog.
/// Keine UI-Abhaengigkeiten.
/// </summary>
public static class VsaCodeEntryValidator
{
    /// <summary>
    /// Prueft einen Quantifizierungswert gegen die zugehoerige Regel.
    /// Gibt null zurueck wenn alles ok, sonst eine Fehlerbeschreibung.
    /// </summary>
    public static string? ValidateQuantField(string value, QuantField? rule)
    {
        if (rule is null) return null;

        if (string.IsNullOrWhiteSpace(value))
            return rule.Pflicht == "P" ? "Pflichtfeld" : null;

        if (!TryParseDouble(value, out var num))
            return "Ungueltige Zahl";

        if (rule.Min.HasValue && num < rule.Min.Value)
            return $">= {rule.Min.Value}";

        if (rule.Max.HasValue && num > rule.Max.Value)
            return $"<= {rule.Max.Value}";

        return null;
    }

    /// <summary>
    /// Prueft ob ein Uhrzeigerstellungs-String gueltig ist (ganze Zahl 0–12).
    /// </summary>
    public static bool IsValidClock(string raw)
    {
        var text = raw.Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
               && v >= 0 && v <= 12;
    }

    /// <summary>
    /// Parst eine Dezimalzahl (erlaubt Komma als Trennzeichen).
    /// </summary>
    public static bool TryParseDouble(string raw, out double value)
    {
        var normalized = raw.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Parst einen Zeitstring (mm:ss oder hh:mm:ss).
    /// </summary>
    public static bool TryParseTime(string raw, out TimeSpan ts)
    {
        ts = default;
        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out ts))
            return true;
        return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out ts);
    }
}
