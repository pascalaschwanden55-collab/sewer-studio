using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Converts a percentage (0-100) to a color gradient: green (low) → yellow (mid) → red (high).
/// Use as a standard IValueConverter bound to the percent property.
/// </summary>
public sealed class PercentToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var percent = value switch
        {
            int i => (double)i,
            double d => d,
            _ => 0.0
        };
        percent = Math.Clamp(percent, 0, 100);

        byte r, g, b;

        if (percent < 50)
        {
            // Green (#22C55E) → Yellow (#EAB308)
            var t = percent / 50.0;
            r = Lerp(0x22, 0xEA, t);
            g = Lerp(0xC5, 0xB3, t);
            b = Lerp(0x5E, 0x08, t);
        }
        else
        {
            // Yellow (#EAB308) → Red (#EF4444)
            var t = (percent - 50) / 50.0;
            r = Lerp(0xEA, 0xEF, t);
            g = Lerp(0xB3, 0x44, t);
            b = Lerp(0x08, 0x44, t);
        }

        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static byte Lerp(byte a, byte b, double t)
        => (byte)(a + (b - a) * t);
}
