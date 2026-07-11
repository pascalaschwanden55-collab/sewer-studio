using System;
using System.Globalization;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>
/// Uhrlagen-Mathematik fuer den Rohrquerschnitt: 12 Uhr = Scheitel (oben, -90 Grad),
/// 3 = rechts, 6 = Sohle, 9 = links; Winkel laufen im Uhrzeigersinn.
/// Pure Statik — testbar ohne WPF (ClockSectorMathTests).
/// </summary>
public static class ClockSectorMath
{
    private const double GradProStunde = 30d;

    /// <summary>Stunde (1..12) in den Zeichnungswinkel in Grad (12 Uhr = -90).</summary>
    public static double HourToAngle(int hour)
        => (hour % 12) * GradProStunde - 90d;

    /// <summary>Beliebiger Winkel in Grad auf die naechste volle Stunde 1..12 gerastet.</summary>
    public static int AngleToHour(double angleDeg)
    {
        // Winkel relativ zu 12 Uhr, im Uhrzeigersinn, auf [0, 360) normalisiert.
        var relativ = (((angleDeg + 90d) % 360d) + 360d) % 360d;
        var stunde = (int)Math.Round(relativ / GradProStunde, MidpointRounding.AwayFromZero) % 12;
        return stunde == 0 ? 12 : stunde;
    }

    /// <summary>Sweep in Grad von VonUhr nach BisUhr im Uhrzeigersinn (10-&gt;2 = 120).
    /// Gleiche Stunde bedeutet ganzer Umfang (360).</summary>
    public static double SweepDegrees(int fromHour, int toHour)
    {
        var stunden = (((toHour - fromHour) % 12) + 12) % 12;
        return stunden == 0 ? 360d : stunden * GradProStunde;
    }

    /// <summary>Toleranter Text-Parser: "10" -&gt; 10, "12:00" -&gt; 12, "13" -&gt; 1
    /// (Zifferblatt-Ueberlauf). "00"/"0" bedeutet KEINE Angabe (VSA-Schnellwahl-Konvention),
    /// leer/unlesbar ebenfalls null.</summary>
    public static int? ParseHour(string? raw)
    {
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        // Fuehrende Ziffern nehmen ("12:00" -> "12"); keine Ziffern -> unlesbar.
        var ende = 0;
        while (ende < text.Length && char.IsDigit(text[ende]))
            ende++;
        if (ende == 0)
            return null;

        if (!int.TryParse(text[..ende], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stunde))
            return null;

        if (stunde == 0)
            return null; // "00" = keine Uhrlage, nicht 12 Uhr.

        var normalisiert = stunde % 12;
        return normalisiert == 0 ? 12 : normalisiert;
    }

    public static string FormatHour(int hour)
        => hour.ToString(CultureInfo.InvariantCulture);
}
