using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

public sealed class SchachtSummaryConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;

        string At(int index) => values is not null && index < values.Length
            ? values[index]?.ToString()?.Trim() ?? ""
            : "";

        var parts = new[] { At(0), At(1), At(2) }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var text = string.Join(" - ", parts);
        return string.IsNullOrWhiteSpace(text) ? "Keine Stammdaten" : text;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();
}

public sealed class SchachtZustandsklasseBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return (object?)ZustandsklasseColorPalette.TryGetBackground(value?.ToString()) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
