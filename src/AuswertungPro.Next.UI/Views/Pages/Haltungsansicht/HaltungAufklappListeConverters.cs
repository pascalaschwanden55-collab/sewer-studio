using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>
/// Nova, Aufklapp-Liste: Ist die Haltung DIESER Zeile die gerade aufgeklappte?
///
/// Die Frage laesst sich nicht aus der Zeile allein beantworten — sie braucht den Datensatz der
/// Zeile UND die aufgeklappte Haltung des Controls. Deshalb eine MultiBinding. Verglichen wird
/// die Instanz, nicht der Haltungsname: Zwei Haltungen duerfen denselben Namen tragen.
/// </summary>
public sealed class HaltungAufgeklapptConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not { Length: 2 })
            return false;

        var zeile = values[0];
        var offen = values[1];
        if (zeile is null || zeile == DependencyProperty.UnsetValue
            || offen is null || offen == DependencyProperty.UnsetValue)
        {
            return false;
        }

        return ReferenceEquals(zeile, offen);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Nova, Aufklapp-Liste: Ist die Liste zu schmal fuer fuenf Themen nebeneinander?
///
/// Unter <see cref="Schwelle"/> Pixeln stehen die Themen des aufgeklappten Bereichs in zwei
/// Spalten statt in einer Zeile — sonst bliebe je Thema keine lesbare Breite uebrig.
/// </summary>
public sealed class SchmaleBreiteConverter : IValueConverter
{
    /// <summary>Ab dieser Breite stehen die Themen nebeneinander (Prototyp v2).</summary>
    public const double Schwelle = 1100;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double breite && breite > 0 && breite < Schwelle;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
