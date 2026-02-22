using System;
using System.Globalization;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Windows;

public sealed class BoolToDoubleConverter : IValueConverter
{
    public double TrueValue { get; set; } = 1.0;
    public double FalseValue { get; set; } = 0.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? TrueValue : FalseValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
