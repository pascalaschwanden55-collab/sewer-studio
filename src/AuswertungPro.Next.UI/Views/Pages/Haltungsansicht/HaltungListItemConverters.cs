using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
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

/// <summary>ProtocolEntry → Meter-Anzeige (z. B. "2.50–8.10 m"); nutzt den getesteten Formatter.</summary>
public sealed class SchadenMeterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ProtocolEntry e ? SchadenZeileFormatter.FormatMeter(e) : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>ProtocolEntry → Klartext (Beschreibung, Fallback Code).</summary>
public sealed class SchadenKlartextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ProtocolEntry e ? SchadenZeileFormatter.Format(e).Klartext : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>ProtocolEntry → Kategorie-Tag ("Bestand"/"Betrieb"/"Zustand"/"").</summary>
public sealed class SchadenKategorieConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ProtocolEntry e ? SchadenZeileFormatter.Kategorie(e.Code) : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
