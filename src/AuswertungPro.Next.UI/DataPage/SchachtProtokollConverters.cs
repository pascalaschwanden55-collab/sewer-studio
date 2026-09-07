using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.4): Sichtbarkeit von Knopf und Gedankenstrich in der
/// Protokollspalte der Schachtliste. Dasselbe Muster wie
/// <see cref="HaltungStatusSichtbarkeitConverter"/>; gerechnet wird nichts hier — die Regel
/// liegt WPF-frei in <see cref="SchachtZeilenStatus"/>.
///
/// Die Bindung haengt am Datensatz UND an seiner Feldkarte: <c>SchachtRecord</c> meldet jede
/// Feldaenderung als <c>Fields</c>, sodass ein neu verknuepftes Protokoll den Knopf sofort
/// erscheinen laesst. Ein einzelnes <c>Fields[...]</c> waere hier falsch — fehlt der
/// Schluessel im Datensatz, lieferte der Indexer nur einen Bindungsfehler.
/// </summary>
public sealed class SchachtProtokollSichtbarkeitConverter : IMultiValueConverter
{
    public const string Protokoll = "Protokoll";
    public const string KeinProtokoll = "KeinProtokoll";

    public static readonly SchachtProtokollSichtbarkeitConverter Instance = new();

    /// <summary>Die Bindungsquellen in fester Reihenfolge; die Fabrik baut daraus die MultiBinding.</summary>
    public static MultiBinding Bindung(string parameter)
    {
        var bindung = new MultiBinding
        {
            Converter = Instance,
            ConverterParameter = parameter,
            Mode = BindingMode.OneWay
        };
        bindung.Bindings.Add(new Binding("."));
        bindung.Bindings.Add(new Binding(nameof(SchachtRecord.Fields)));
        return bindung;
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not { Length: > 0 } || values[0] is not SchachtRecord record)
            return Visibility.Collapsed;

        var hatProtokoll = SchachtZeilenStatus.HatProtokoll(record);
        var sichtbar = (parameter as string) switch
        {
            Protokoll => hatProtokoll,
            KeinProtokoll => !hatProtokoll,
            _ => false
        };
        return sichtbar ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
