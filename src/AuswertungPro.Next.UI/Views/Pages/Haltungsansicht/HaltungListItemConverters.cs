using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>Bindet das ganze HaltungRecord-Item auf die einzeilige Kurzbeschreibung.</summary>
public sealed class HaltungSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is HaltungRecord r ? HaltungSummaryFormatter.FormatSummary(r) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Bindet den Zustandsklasse-Text auf den Chip-Hintergrund (gleiche Quelle wie die Tabelle).</summary>
public sealed class ZustandsklasseBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (object?)ZustandsklasseColorPalette.TryGetBackground(value?.ToString()) ?? DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
