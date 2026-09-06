using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Zustandsklasse-Text -> Textfarbe passend zum Marken-Hintergrund derselben Klasse.</summary>
public sealed class ZustandsklasseInkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (ZustandsklasseColorPalette.TryGetBackground(value?.ToString()) is not SolidColorBrush bg)
            return DependencyProperty.UnsetValue;

        var brush = new SolidColorBrush(ZustandsklasseInkPolicy.InkFor(bg.Color));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
