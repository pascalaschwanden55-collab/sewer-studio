using System;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Schaltet "Streckenschaden schliessen" nur fuer eine Zeile frei, die wirklich ein
/// offener Anfang ist. Ohne diese Grenze war der Menuepunkt bei jeder Zeile aktiv -
/// auch bei einem Punktschaden, der dadurch still zum Streckenschaden geworden waere.
///
/// Erster Wert: die ausgewaehlte Zeile (loest die Neubewertung aus).
/// Zweiter Wert: die Liste selbst - die Endmarke eines Streckenschadens ist nur
/// zusammen mit ihrem Anfang erkennbar.
/// </summary>
public sealed class CodingStretchDamageCanCloseConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2)
            return false;

        if (values[0] is not CodingEvent selected)
            return false;

        var allEvents = values[1] is ListBox list
            ? list.Items.OfType<CodingEvent>().ToList()
            : null;

        return CodingStretchDamageDisplayPolicy.CanClose(selected, allEvents);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
