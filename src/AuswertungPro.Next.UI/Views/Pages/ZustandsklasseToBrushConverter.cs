using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

public sealed class ZustandsklasseToBrushConverter : IValueConverter
{
    public static readonly ZustandsklasseToBrushConverter Instance = new();
    private static readonly Brush OhneBrush = FrozenBrush(142, 150, 162);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value?.ToString();
        if (string.Equals(key, "ohne", StringComparison.OrdinalIgnoreCase))
            return OhneBrush;

        return ZustandsklasseColorPalette.TryGetBackground(key) ?? OhneBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush FrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
