using System;
using System.Globalization;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Skalierung der Kostenbalken in der Sanierungs-Matrix: Balkenbreite
/// proportional zum teuersten Zeilen-Total (Maximum), mit 2px Minimum
/// fuer kleine positive Betraege.
/// </summary>
public static class KostenBalkenScale
{
    private const double MinBreite = 2.0;

    public static double Anteil(decimal total, decimal maxTotal)
    {
        if (total <= 0m || maxTotal <= 0m)
            return 0.0;
        return Math.Min(1.0, (double)(total / maxTotal));
    }

    public static double Breite(decimal total, decimal maxTotal, double verfuegbareBreite)
    {
        var anteil = Anteil(total, maxTotal);
        if (anteil <= 0.0 || verfuegbareBreite <= 0.0)
            return 0.0;
        return Math.Max(MinBreite, anteil * verfuegbareBreite);
    }
}

/// <summary>
/// MultiValueConverter fuer die Balken-Spalte:
/// [0] Zeilen-Total (decimal), [1] MaxRowTotal (decimal), [2] verfuegbare Breite (double).
/// </summary>
public sealed class KostenBalkenBreiteConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3
            || values[0] is not decimal total
            || values[1] is not decimal max
            || values[2] is not double breite)
            return 0.0;

        return KostenBalkenScale.Breite(total, max, breite);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
