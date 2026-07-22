using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Zeigt ein Element nur, wenn der gebundene Enum-Wert dem ConverterParameter entspricht
/// (Vergleich ueber den Namen, damit der Zielzustand in XAML als Text steht). Sonst
/// <see cref="Visibility.Collapsed"/>. Wird vom <see cref="StatusHost"/>-Template genutzt,
/// ist aber bewusst allgemein gehalten.
/// </summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Matches(value, parameter) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>Reine Vergleichslogik — ohne WPF testbar.</summary>
    public static bool Matches(object? value, object? parameter)
        => value is not null
           && parameter is not null
           && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
}
