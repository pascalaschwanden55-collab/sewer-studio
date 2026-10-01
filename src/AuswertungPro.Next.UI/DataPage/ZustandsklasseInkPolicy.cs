using System;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Textfarbe auf einer Zustandsklassen-Marke. Ziel aus dem Nova-Prototyp: jede Marke
/// erreicht mindestens 4,5:1 (WCAG normaler Text). Dunkle Tinte wird bevorzugt, weil sie auf
/// Gelb, Oliv, Orange und Gruen sicher reicht; nur wenn sie nicht reicht, wird Weiss verwendet.
/// </summary>
public static class ZustandsklasseInkPolicy
{
    public static readonly Color DarkInk = Color.FromRgb(0x0B, 0x12, 0x20);
    public static readonly Color LightInk = Colors.White;
    private const double Ziel = 4.5;

    public static Color InkFor(Color background)
        => Contrast(DarkInk, background) >= Ziel ? DarkInk : LightInk;

    /// <summary>WCAG-Kontrast zweier deckender Farben (1 bis 21).</summary>
    public static double Contrast(Color a, Color b)
    {
        var la = Luminanz(a);
        var lb = Luminanz(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminanz(Color c)
    {
        static double Kanal(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Kanal(c.R) + 0.7152 * Kanal(c.G) + 0.0722 * Kanal(c.B);
    }
}
