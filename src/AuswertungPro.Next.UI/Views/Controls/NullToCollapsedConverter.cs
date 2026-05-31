using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>Bindet einen Wert auf Visibility: null -> Collapsed, sonst Visible (Einweg).</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
