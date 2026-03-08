using System;
using System.Globalization;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// MultiBinding converter: takes (percent, containerWidth) and returns pixel width.
/// Used for slim progress bar rendering in the system resource monitor.
/// </summary>
public sealed class PercentToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0.0;

        var percent = values[0] switch
        {
            int i => (double)i,
            double d => d,
            _ => 0.0
        };

        var containerWidth = values[1] switch
        {
            double d => d,
            _ => 0.0
        };

        return Math.Max(0, Math.Min(containerWidth, containerWidth * percent / 100.0));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
