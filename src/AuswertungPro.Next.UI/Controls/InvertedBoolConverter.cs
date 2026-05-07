using System;
using System.Globalization;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Bindet eine Boolean-Property invertiert (z.B. <c>IsEnabled = !IsRunning</c>).
/// Verwendung: <c>{Binding IsRunning, Converter={StaticResource InvertedBoolConverter}}</c>.
/// </summary>
public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
