using System.Globalization;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Reine Parsing- und Normalisierungs-Logik fuer Protokoll-Eingabefelder.
/// Kein UI-Bezug – alle Methoden sind statisch und ohne Seiteneffekte.
/// </summary>
public static class ProtocolEntryInputNormalizer
{
    /// <summary>Code auf Grossbuchstaben normieren und Leerzeichen entfernen.</summary>
    public static string NormalizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Concat(value
            .Trim()
            .ToUpperInvariant()
            .Where(ch => !char.IsWhiteSpace(ch)));
    }

    /// <summary>
    /// Optionalen Double-Wert parsen (leer = null, ok). Komma als Dezimaltrenner erlaubt.
    /// Gibt false zurueck, wenn der Text nicht leer und nicht parsebar ist.
    /// </summary>
    public static bool TryParseOptionalDouble(string raw, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var normalized = raw.Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return false;

        value = parsed;
        return true;
    }

    /// <summary>
    /// Optionalen TimeSpan-Wert parsen (leer = null, ok).
    /// Akzeptiert mm:ss, hh:mm:ss sowie TimeSpan-Standardformat.
    /// </summary>
    public static bool TryParseOptionalTimeSpan(string raw, out TimeSpan? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Fallback: Versucht TimeSpan zu parsen, gibt null zurueck wenn nicht moeglich.
    /// </summary>
    public static TimeSpan? TryParseTimeFallback(string raw)
    {
        if (TryParseOptionalTimeSpan(raw, out var value))
            return value;
        return null;
    }

    /// <summary>
    /// Uhrzeigerposition normieren (0-12 als zweistellige Zeichenkette).
    /// Leer ist erlaubt (hasValue = false, result = true).
    /// </summary>
    public static bool TryNormalizeClockPosition(string? raw, out string normalized, out bool hasValue)
    {
        normalized = string.Empty;
        hasValue = !string.IsNullOrWhiteSpace(raw);
        if (!hasValue)
            return true;

        var text = raw!.Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return false;
        if (value < 0 || value > 12)
            return false;

        normalized = value.ToString("00", CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>
    /// VSA-Strecke normieren (A/B/C + Ziffer, z.B. A1, B2).
    /// Leer ist erlaubt (hasValue = false, result = true).
    /// </summary>
    public static bool TryNormalizeStrecke(string? raw, out string normalized, out bool hasValue)
    {
        normalized = string.Empty;
        hasValue = !string.IsNullOrWhiteSpace(raw);
        if (!hasValue)
            return true;

        var text = raw!.Trim().ToUpperInvariant();
        if (text.Length == 1 && (text == "A" || text == "B" || text == "C"))
        {
            normalized = text + "1";
            return true;
        }

        if (text.Length >= 2 && (text[0] == 'A' || text[0] == 'B' || text[0] == 'C') && text.Skip(1).All(char.IsDigit))
        {
            normalized = text;
            return true;
        }

        return false;
    }

    /// <summary>
    /// VSA-EZ-Wert normieren (EZ0 bis EZ4).
    /// Leer ist erlaubt (hasValue = false, result = true).
    /// </summary>
    public static bool TryNormalizeEz(string? raw, out string normalized, out bool hasValue)
    {
        normalized = string.Empty;
        hasValue = !string.IsNullOrWhiteSpace(raw);
        if (!hasValue)
            return true;

        var text = raw!.Trim().ToUpperInvariant();
        if (text.StartsWith("EZ", StringComparison.Ordinal))
            text = text.Substring(2);

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return false;
        if (value < 0 || value > 4)
            return false;

        normalized = $"EZ{value}";
        return true;
    }

    /// <summary>
    /// VSA-Schachtbereich normieren (A/B/D/F/H/I/J).
    /// Leer ist erlaubt (hasValue = false, result = true).
    /// </summary>
    public static bool TryNormalizeSchachtbereich(string? raw, out string normalized, out bool hasValue)
    {
        normalized = string.Empty;
        hasValue = !string.IsNullOrWhiteSpace(raw);
        if (!hasValue)
            return true;

        normalized = raw!.Trim().ToUpperInvariant();
        return normalized is "A" or "B" or "D" or "F" or "H" or "I" or "J";
    }

    /// <summary>
    /// Optionalen Int-Wert parsen (leer = null, ok).
    /// Gibt false zurueck, wenn der Text nicht leer und nicht parsebar ist.
    /// </summary>
    public static bool TryParseOptionalInt(string raw, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return false;

        value = parsed;
        return true;
    }

    /// <summary>
    /// Optionalen Double-Wert als "0.00"-String formatieren (null ergibt Leerstring).
    /// </summary>
    public static string FormatDouble(double? value)
        => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>
    /// TimeSpan als Anzeigestring formatieren (mm:ss oder hh:mm:ss je nach Laenge).
    /// </summary>
    public static string FormatTime(TimeSpan value)
        => value.TotalHours >= 1
            ? value.ToString(@"hh\:mm\:ss")
            : value.ToString(@"mm\:ss");
}
