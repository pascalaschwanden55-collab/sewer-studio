using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Pages;

internal static class ZustandsklasseCellStyleFactory
{
    private static readonly Brush ForegroundBrush = CreateBrush(0x00, 0x00, 0x00);

    // Colors taken from Excel template "Haltungen.xlsx"
    private static readonly IReadOnlyDictionary<string, Brush> HaltungenPalette = new Dictionary<string, Brush>(StringComparer.Ordinal)
    {
        ["0"] = CreateBrush(0xFF, 0x00, 0x00),
        ["1"] = CreateBrush(0xFF, 0x66, 0x00),
        ["2"] = CreateBrush(0xFF, 0xFF, 0x00),
        ["3"] = CreateBrush(0xAE, 0xB1, 0x35),
        ["4"] = CreateBrush(0x92, 0xD0, 0x50)
    };

    // Colors taken from Excel template "Schaechte/Schächte.xlsx"
    private static readonly IReadOnlyDictionary<string, Brush> SchaechtePalette = new Dictionary<string, Brush>(StringComparer.Ordinal)
    {
        ["0"] = CreateBrush(0xFF, 0x00, 0x00),
        ["1"] = CreateBrush(0xFF, 0x66, 0x00),
        ["2"] = CreateBrush(0xFF, 0xFF, 0x00),
        ["3"] = CreateBrush(0xA5, 0xA8, 0x32),
        ["4"] = CreateBrush(0x92, 0xD0, 0x50)
    };

    private static readonly IValueConverter HaltungenBackgroundConverter = new PaletteToBackgroundConverter(HaltungenPalette);
    private static readonly IValueConverter SchaechteBackgroundConverter = new PaletteToBackgroundConverter(SchaechtePalette);
    private static readonly IValueConverter HaltungenForegroundConverter = new PaletteToForegroundConverter(HaltungenPalette, ForegroundBrush);
    private static readonly IValueConverter SchaechteForegroundConverter = new PaletteToForegroundConverter(SchaechtePalette, ForegroundBrush);
    private static readonly IValueConverter EigentuemerBackgroundConverter = new TextPaletteToBackgroundConverter(MapEigentuemerBackground);
    private static readonly IValueConverter EigentuemerForegroundConverter = new TextPaletteToForegroundConverter(MapEigentuemerBackground, ForegroundBrush);
    private static readonly IValueConverter PruefungsresultatBackgroundConverter = new TextPaletteToBackgroundConverter(MapPruefungsresultatBackground);
    private static readonly IValueConverter PruefungsresultatForegroundConverter = new TextPaletteToForegroundConverter(MapPruefungsresultatBackground, ForegroundBrush);
    private static readonly IValueConverter AusgefuehrtDurchBackgroundConverter = new TextPaletteToBackgroundConverter(MapAusgefuehrtDurchBackground);
    private static readonly IValueConverter AusgefuehrtDurchForegroundConverter = new TextPaletteToForegroundConverter(MapAusgefuehrtDurchBackground, ForegroundBrush);

    public static Style CreateHaltungenStyle(string fieldName)
        => CreateStyle(fieldName, HaltungenBackgroundConverter, HaltungenForegroundConverter);

    public static Style CreateSchaechteStyle(string fieldName)
        => CreateStyle(fieldName, SchaechteBackgroundConverter, SchaechteForegroundConverter);

    public static Style CreateEigentuemerStyle(string fieldName)
        => CreateStyle(fieldName, EigentuemerBackgroundConverter, EigentuemerForegroundConverter);

    public static Style CreatePruefungsresultatStyle(string fieldName)
        => CreateStyle(fieldName, PruefungsresultatBackgroundConverter, PruefungsresultatForegroundConverter);

    public static Style CreateAusgefuehrtDurchStyle(string fieldName)
        => CreateStyle(fieldName, AusgefuehrtDurchBackgroundConverter, AusgefuehrtDurchForegroundConverter);

    private static Style CreateStyle(
        string fieldName,
        IValueConverter backgroundConverter,
        IValueConverter foregroundConverter)
    {
        var baseStyle = System.Windows.Application.Current.TryFindResource(typeof(DataGridCell)) as Style;
        var style = baseStyle is null
            ? new Style(typeof(DataGridCell))
            : new Style(typeof(DataGridCell), baseStyle);
        style.Setters.Add(new Setter(Control.BackgroundProperty, new Binding($"Fields[{fieldName}]")
        {
            Mode = BindingMode.OneWay,
            Converter = backgroundConverter
        }));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new Binding($"Fields[{fieldName}]")
        {
            Mode = BindingMode.OneWay,
            Converter = foregroundConverter
        }));
        return style;
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Brush? MapEigentuemerBackground(string value)
    {
        var key = NormalizeText(value);
        if (key.Length == 0)
            return null;

        // Priority exactly follows the provided legend.
        if (key.Contains("kanton", StringComparison.Ordinal))
            return CreateBrush(0xFF, 0xFF, 0x00); // yellow
        if (key.Contains("bund", StringComparison.Ordinal))
            return CreateBrush(0xFF, 0x80, 0x00); // orange
        if (key.Contains("gemeinde", StringComparison.Ordinal))
            return CreateBrush(0x00, 0xB0, 0xF0); // blue/cyan
        if (key.Contains("awu", StringComparison.Ordinal))
            return CreateBrush(0x54, 0x82, 0x35); // green
        if (key.Contains("privat", StringComparison.Ordinal))
            return CreateBrush(0xFF, 0x00, 0x00); // red

        return null;
    }

    private static Brush? MapPruefungsresultatBackground(string value)
    {
        var key = NormalizeText(value);
        if (key.Length == 0)
            return null;

        // Prüfung bestanden / knapp nicht bestanden / nicht bestanden (grob undicht) / Keine
        if (key.Contains("keine", StringComparison.Ordinal))
            return CreateBrush(0xE7, 0xE6, 0xE6); // light gray
        if (key.Contains("grob", StringComparison.Ordinal) || key.Contains("undicht", StringComparison.Ordinal))
            return CreateBrush(0xFF, 0x00, 0x00); // red
        if (key.Contains("knapp", StringComparison.Ordinal))
            return CreateBrush(0xFF, 0xFF, 0x00); // yellow
        if (key.Contains("nicht bestanden", StringComparison.Ordinal))
            return CreateBrush(0xFF, 0x00, 0x00); // red
        if (key.Contains("bestanden", StringComparison.Ordinal))
            return CreateBrush(0x92, 0xD0, 0x50); // green

        return null;
    }

    private static Brush? MapAusgefuehrtDurchBackground(string value)
    {
        var key = NormalizeText(value);
        if (key.Length == 0)
            return null;

        // Blue should win for "Baumeister".
        if (key.Contains("baumeister", StringComparison.Ordinal))
            return CreateBrush(0x00, 0xB0, 0xF0); // blue/cyan
        if (key.Contains("kanalsanierer", StringComparison.Ordinal) || key.Contains("sanierer", StringComparison.Ordinal))
            return CreateBrush(0xBF, 0x8F, 0x00); // ochre

        return null;
    }

    private static string NormalizeText(string value)
    {
        var text = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("Ã¤", "ä", StringComparison.Ordinal)
            .Replace("Ã¶", "ö", StringComparison.Ordinal)
            .Replace("Ã¼", "ü", StringComparison.Ordinal)
            .Replace("ÃŸ", "ß", StringComparison.Ordinal);

        if (text.Length == 0)
            return string.Empty;

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        text = sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Replace("ue", "u", StringComparison.Ordinal)
            .Replace("ae", "a", StringComparison.Ordinal)
            .Replace("oe", "o", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);

        return text;
    }

    private static string NormalizeClass(object? value)
    {
        var text = (value?.ToString() ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        if (text.Length >= 1 && char.IsDigit(text[0]))
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

    private sealed class PaletteToBackgroundConverter(IReadOnlyDictionary<string, Brush> palette) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = NormalizeClass(value);
            return palette.TryGetValue(key, out var brush) ? brush : DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    private sealed class PaletteToForegroundConverter(IReadOnlyDictionary<string, Brush> palette, Brush foreground) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = NormalizeClass(value);
            return palette.ContainsKey(key) ? foreground : DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    private sealed class TextPaletteToBackgroundConverter(Func<string, Brush?> resolver) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var brush = resolver(value?.ToString() ?? string.Empty);
            return brush ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    private sealed class TextPaletteToForegroundConverter(Func<string, Brush?> resolver, Brush foreground) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var brush = resolver(value?.ToString() ?? string.Empty);
            return brush is null ? DependencyProperty.UnsetValue : foreground;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
