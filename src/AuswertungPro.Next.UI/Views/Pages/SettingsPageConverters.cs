using System;
using System.Globalization;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13: bindet die Design-Wahl (Hell/Dunkel/"Wie Windows",
/// <see cref="AuswertungPro.Next.UI.Services.ThemeManager"/>) an den IsChecked-Zustand je eines
/// Radioknopfs. ConverterParameter = Zielwert ("Light"/"Dark"/"System"), gleiches Muster wie
/// <c>DistributionVariantToBoolConverter</c> auf der Exportseite.
/// </summary>
public sealed class ThemePreferenceToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string current
           && parameter is string target
           && string.Equals(current, target, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is string target
            ? target
            : Binding.DoNothing;
}
