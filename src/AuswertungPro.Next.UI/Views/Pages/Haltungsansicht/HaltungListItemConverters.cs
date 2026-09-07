using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
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

/// <summary>ProtocolEntry → Code (Badge in der Schadensliste der Uebersicht).</summary>
public sealed class SchadenCodeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ProtocolEntry e ? e.Code : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// ProtocolEntry → zweite Zeile der Schadensliste: Stufe, Quelle (fachlich/KI) und Freigabestatus.
/// Die Textbildung liegt als testbare statische Methode vor.
/// </summary>
public sealed class SchadenStufeQuelleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ProtocolEntry e ? Text(e) : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;

    /// <summary>"Stufe n · KI-Vorschlag, Konfidenz 0.91 (Modellsicherheit) · offen" bzw. " · fachlich erfasst".</summary>
    internal static string Text(ProtocolEntry e)
    {
        var teile = new List<string>();
        if (e.CodeMeta?.Severity is { Length: > 0 } stufe)
            teile.Add($"Stufe {stufe}");
        teile.Add(e.Ai is not null
            ? $"KI-Vorschlag, Konfidenz {e.Ai.Confidence.ToString("0.00", CultureInfo.InvariantCulture)} (Modellsicherheit)"
            : "fachlich erfasst");
        if (e.Ai is { Accepted: false })
            teile.Add("offen");
        else if (e.Ai is { Accepted: true })
            teile.Add("bestätigt");
        return string.Join(" · ", teile);
    }
}

/// <summary>Zahl groesser 0 -> sichtbar, sonst eingeklappt (KI-Hinweis nur bei offenen Befunden).</summary>
public sealed class ZahlSichtbarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// Ein Eckdatenwert der Uebersicht; leer wird zum Gedankenstrich. Der optionale
/// ConverterParameter ist die Einheit ("m").
///
/// Task 6: Ohne gewaehlten Datensatz liefert WPF <see cref="DependencyProperty.UnsetValue"/>.
/// Dessen ToString() ist der sichtbare Fehltext "{DependencyProperty.UnsetValue}" — deshalb
/// wird er zuerst zu null, bevor die WPF-freie Regel ihn sieht.
/// </summary>
public sealed class FaktWertConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => HaltungFaktenText.Wert(FaktWerte.AlsText(value), parameter?.ToString());
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Mehrere Eckdatenwerte in einer Zelle ("300 · Kreisprofil"); alle leer = Gedankenstrich.</summary>
public sealed class FaktZusammenConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => HaltungFaktenText.Zusammen((values ?? Array.Empty<object>()).Select(FaktWerte.AlsText));

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();
}

/// <summary>Gemeinsame Leseregel der beiden Eckdaten-Konverter.</summary>
internal static class FaktWerte
{
    /// <summary>
    /// Der Text eines Bindungswerts. Eine nicht gesetzte Bindung
    /// (<see cref="DependencyProperty.UnsetValue"/>) ist kein Wert und wird zu null; die
    /// Uebersicht zeigt dafuer denselben Gedankenstrich wie fuer ein leeres Feld.
    /// </summary>
    internal static string? AlsText(object? wert)
        => wert is null || wert == DependencyProperty.UnsetValue ? null : wert.ToString();
}
