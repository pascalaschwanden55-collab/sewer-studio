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
/// liegt WPF-frei in <see cref="SchachtProtokollQuelle"/>, dieselbe, nach der der Oeffner die
/// Datei sucht.
/// </summary>
public sealed class SchachtProtokollSichtbarkeitConverter : IMultiValueConverter
{
    public const string Protokoll = "Protokoll";
    public const string KeinProtokoll = "KeinProtokoll";

    public static readonly SchachtProtokollSichtbarkeitConverter Instance = new();

    /// <summary>Die Bindungsquellen in fester Reihenfolge; die Fabrik baut daraus die MultiBinding.</summary>
    public static MultiBinding Bindung(string parameter) => Bindung(Instance, parameter);

    /// <summary>
    /// Dieselben Quellen fuer jeden Konverter der Protokollspalte: der Datensatz und seine
    /// Feldkarte. <c>SchachtRecord</c> meldet jede Feldaenderung als <c>Fields</c>, sodass ein
    /// neu verknuepftes Protokoll den Knopf sofort erscheinen laesst. Ein einzelnes
    /// <c>Fields[...]</c> waere hier falsch — fehlt der Schluessel im Datensatz, lieferte der
    /// Indexer nur einen Bindungsfehler.
    /// </summary>
    public static MultiBinding Bindung(IMultiValueConverter converter, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var bindung = new MultiBinding
        {
            Converter = converter,
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

        var hatProtokoll = SchachtProtokollQuelle.Vorhanden(record);
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

/// <summary>
/// Nova-Etappe 2b, Task 6 (Fix-Runde 1): Der vorlesbare Name des Protokollknopfs. Die
/// Schachtnummer kommt aus derselben Regel wie auf der Seite
/// (<see cref="SchaechteColumnPolicy.GetSchachtNumber"/>) — sie steht je nach Projekt unter
/// "Schachtnummer", "Nr." oder "NR.". Ein hart gelesenes Feld haette dort eine Luecke im Satz
/// hinterlassen.
/// </summary>
public sealed class SchachtProtokollNameConverter : IMultiValueConverter
{
    public static readonly SchachtProtokollNameConverter Instance = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values is { Length: > 0 } && values[0] is SchachtRecord record ? Name(record) : Name(null);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>"Protokoll 78998 öffnen"; ohne bekannte Nummer schlicht "Protokoll öffnen".</summary>
    internal static string Name(SchachtRecord? record)
    {
        var nummer = record is null ? string.Empty : SchaechteColumnPolicy.GetSchachtNumber(record);
        return string.IsNullOrWhiteSpace(nummer) ? "Protokoll öffnen" : $"Protokoll {nummer} öffnen";
    }
}
