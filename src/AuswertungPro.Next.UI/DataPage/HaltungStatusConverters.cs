using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3): Liefert den Zeilenstatus einer Haltung an die Statusspalten.
///
/// Die Bindung ist bewusst eine <see cref="MultiBinding"/> auf den Datensatz selbst, auf das
/// Feld "offen/abgeschlossen" und auf das Protokoll: Nur so meldet sich die Zelle bei einer
/// Feldaenderung UND bei einem In-Place-Ersatz des Protokolls neu. Gerechnet wird nichts hier —
/// die Regel liegt WPF-frei in <see cref="HaltungZeilenStatus"/>.
/// </summary>
public sealed class HaltungZeilenStatusConverter : IMultiValueConverter
{
    public static readonly HaltungZeilenStatusConverter Instance = new();

    /// <summary>
    /// Die Bindungsquellen in fester Reihenfolge; die Tabelle baut daraus ihre MultiBinding.
    ///
    /// Die Aufklapp-Liste (<c>HaltungAufklappListe.xaml</c>) deklariert dieselben drei Quellen
    /// direkt im XAML, weil ihre Zellen dort stehen und nicht im Code entstehen. Wer hier eine
    /// Quelle ergaenzt, muss sie auch dort ergaenzen — sonst meldet sich die eine Ansicht bei
    /// einer Aenderung neu und die andere nicht.
    /// </summary>
    public static MultiBinding Bindung(IMultiValueConverter converter, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(converter);
        var bindung = new MultiBinding { Converter = converter, ConverterParameter = parameter, Mode = BindingMode.OneWay };
        bindung.Bindings.Add(new Binding("."));
        bindung.Bindings.Add(new Binding($"Fields[{FieldKeys.WorkflowStatus}]"));
        bindung.Bindings.Add(new Binding(nameof(HaltungRecord.Protocol)));
        return bindung;
    }

    /// <summary>
    /// Der Datensatz steht an erster Stelle; die uebrigen Werte dienen nur als Ausloeser.
    /// <see cref="DependencyProperty.UnsetValue"/> (z. B. waehrend des Zeilenaufbaus oder bei
    /// der leeren Neuzeile des DataGrid) wird ignoriert statt geraten.
    /// </summary>
    public static HaltungZeilenStatusErgebnis? Bestimme(object[]? values)
        => values is { Length: > 0 } && values[0] is HaltungRecord record
            ? HaltungZeilenStatus.Bestimme(record)
            : null;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => (object?)Bestimme(values) ?? DependencyProperty.UnsetValue;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Nova-Etappe 2b: Sichtbarkeit der Knoepfe in den Spalten Video und Protokoll.
///
/// Video und Protokoll brauchen den Datensatz selbst als Befehlsparameter; ihre Zellvorlage
/// bleibt deshalb am Datensatz gebunden und holt sich nur diese eine Ja/Nein-Auskunft ueber
/// einen Konverter — statt wie KI und Pruefung den ganzen Zeilenstatus als Inhalt zu tragen.
/// </summary>
public sealed class HaltungStatusSichtbarkeitConverter : IMultiValueConverter
{
    public const string Video = "Video";
    public const string KeinVideo = "KeinVideo";
    public const string Protokoll = "Protokoll";
    public const string KeinProtokoll = "KeinProtokoll";

    public static readonly HaltungStatusSichtbarkeitConverter Instance = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var status = HaltungZeilenStatusConverter.Bestimme(values);
        if (status is null)
            return Visibility.Collapsed;

        var sichtbar = (parameter as string) switch
        {
            Video => status.HatVideo,
            KeinVideo => !status.HatVideo,
            Protokoll => status.HatProtokoll,
            KeinProtokoll => !status.HatProtokoll,
            _ => false
        };
        return sichtbar ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Nova-Etappe 2b: Zeigt genau das Element, dessen Parameter zum aktuellen Aufzaehlungswert
/// passt (KI-Ampel, Pruefstand).
///
/// Warum nicht ein Element mit DataTriggern: Farben duerfen ausserhalb des Themes nur als
/// <c>DynamicResource</c> gelesen werden, damit ein Themenwechsel zur Laufzeit durchschlaegt.
/// In einer im Code gebauten Zellvorlage ist <c>SetResourceReference</c> der belegte Weg dafuer;
/// er haengt am Element, nicht an einem Stil-Setter. Deshalb liegt je Zustand ein eigenes,
/// fertig eingefaerbtes Element in der Zelle, und nur eines davon ist sichtbar.
/// </summary>
public sealed class AufzaehlungSichtbarkeitConverter : IValueConverter
{
    public static readonly AufzaehlungSichtbarkeitConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter as string, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
