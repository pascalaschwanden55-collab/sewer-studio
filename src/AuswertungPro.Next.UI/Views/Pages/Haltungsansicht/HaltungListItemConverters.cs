using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>
/// Baut die einzeilige Kurzbeschreibung aus den drei Feldwerten (DN, Laenge, Nutzungsart).
/// MultiBinding auf die Feld-Pfade, damit die Zeile live aktualisiert, wenn ein Feld geaendert wird.
/// </summary>
public sealed class HaltungSummaryConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        string? At(int i) => values is not null && i < values.Length ? values[i] as string : null;
        return HaltungSummaryFormatter.FormatSummary(At(0), At(1), At(2));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();
}

/// <summary>Bindet den Zustandsklasse-Text auf den Chip-Hintergrund (gleiche Quelle wie die Tabelle).</summary>
public sealed class ZustandsklasseBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (object?)ZustandsklasseColorPalette.TryGetBackground(value?.ToString()) ?? DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
