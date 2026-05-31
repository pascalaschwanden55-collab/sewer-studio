using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Eine Quelle für die Zustandsklasse-Farben (Skala 0=rot … 4=grün).
/// Wird von der Tabellen-Zellfarbe (ZustandsklasseCellStyleFactory) und vom
/// Listen-Chip der Haltungsansicht genutzt. Farben aus Excel-Vorlage "Haltungen.xlsx".
/// </summary>
public static class ZustandsklasseColorPalette
{
    public static IReadOnlyDictionary<string, Brush> HaltungenPalette { get; } =
        new Dictionary<string, Brush>(StringComparer.Ordinal)
        {
            ["0"] = CreateBrush(0xFF, 0x00, 0x00),
            ["1"] = CreateBrush(0xFF, 0x66, 0x00),
            ["2"] = CreateBrush(0xFF, 0xFF, 0x00),
            ["3"] = CreateBrush(0xAE, 0xB1, 0x35),
            ["4"] = CreateBrush(0x92, 0xD0, 0x50)
        };

    /// <summary>Hintergrund-Brush für eine Zustandsklasse, oder null wenn unbekannt/leer.</summary>
    public static Brush? TryGetBackground(string? value)
    {
        var key = NormalizeClass(value);
        return HaltungenPalette.TryGetValue(key, out var brush) ? brush : null;
    }

    /// <summary>Normalisiert "0".."4", Dezimalwerte (gerundet) und Komma-Dezimale; sonst "".</summary>
    public static string NormalizeClass(object? value)
    {
        var text = (value?.ToString() ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        if (char.IsDigit(text[0]))
        {
            var digit = text[0];
            return digit is >= '0' and <= '4' ? digit.ToString() : string.Empty;
        }

        var normalized = text.Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return string.Empty;

        var rounded = (int)Math.Round(number, MidpointRounding.AwayFromZero);
        return rounded is >= 0 and <= 4 ? rounded.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
