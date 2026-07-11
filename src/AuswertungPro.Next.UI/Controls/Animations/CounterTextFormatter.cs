using System;
using System.Globalization;

namespace AuswertungPro.Next.UI.Controls.Animations;

/// <summary>
/// Formatiert den Wert des animierten Zaehlers. Vom Control getrennt, damit testbar.
/// Unterstuetzt numerische Formate ("N0", "0.0") und Composite-Formate ("{0:N0} m").
/// </summary>
public static class CounterTextFormatter
{
    public static string Format(double value, string? stringFormat, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(stringFormat))
            return value.ToString(culture);

        try
        {
            return stringFormat.Contains('{')
                ? string.Format(culture, stringFormat, value)
                : value.ToString(stringFormat, culture);
        }
        catch (FormatException)
        {
            // Kaputtes Format darf nie die UI reissen — nackte Zahl zeigen.
            return value.ToString(culture);
        }
    }
}
