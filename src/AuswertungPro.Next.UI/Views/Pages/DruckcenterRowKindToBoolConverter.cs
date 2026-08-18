using System;
using System.Globalization;
using System.Windows.Data;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Bindet den Druckcenter-Bereich (<see cref="DruckcenterRowKind"/>) an den IsChecked-Zustand
/// eines Umschalt-Knopfs. ConverterParameter = Zielbereich ("Haltung"/"Schacht").
/// Gleiches Muster wie der Normal/Sanierung-Umschalter der Export-Seite.
/// </summary>
public sealed class DruckcenterRowKindToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DruckcenterRowKind kind
           && parameter is string p
           && string.Equals(kind.ToString(), p, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Nur das Anhaken setzt den Bereich. Beim Abhaken bewusst <see cref="Binding.DoNothing"/>:
    /// Sonst wuerde der abgewaehlte Knopf den soeben gesetzten Bereich zurueckschreiben.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
           && parameter is string p
           && Enum.TryParse<DruckcenterRowKind>(p, true, out var kind)
            ? kind
            : Binding.DoNothing;
}
