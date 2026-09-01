using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
using AuswertungPro.Next.Infrastructure.Export.Excel;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Eine Quelle für die Zustandsklasse-Farben (Skala 0=rot … 4=grün).
/// Wird von der Tabellen-Zellfarbe (ZustandsklasseCellStyleFactory) und vom
/// Listen-Chip der Haltungsansicht genutzt. Farben aus Excel-Vorlage "Haltungen.xlsx".
/// </summary>
public static class ZustandsklasseColorPalette
{
    public static IReadOnlyList<string> SelectionOptions { get; } = ["0", "1", "2", "3", "4"];

    public static IReadOnlyDictionary<string, Brush> HaltungenPalette { get; } =
        ExcelReportStyle.Zustandsklassen.ToDictionary(
            rule => rule.Wert,
            rule => (Brush)CreateBrush(rule.Farbe),
            StringComparer.Ordinal);

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

    private static SolidColorBrush CreateBrush(string argb)
    {
        if (argb.Length != 8)
            throw new InvalidOperationException($"Ungueltige ARGB-Farbe: {argb}");

        var brush = new SolidColorBrush(Color.FromRgb(
            Convert.ToByte(argb.Substring(2, 2), 16),
            Convert.ToByte(argb.Substring(4, 2), 16),
            Convert.ToByte(argb.Substring(6, 2), 16)));
        brush.Freeze();
        return brush;
    }
}
