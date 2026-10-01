using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class ProjektUebersichtPage : UserControl
{
    public ProjektUebersichtPage()
    {
        InitializeComponent();
    }
}

/// <summary>
/// Balkenbreite aus einem Anteil und der verfuegbaren Breite. Ohne
/// <c>ConverterParameter</c> ist der erste Wert bereits ein Bruchteil (0..1, z. B.
/// Schadenshaeufigkeit); mit <c>ConverterParameter="prozent"</c> ist er 0..100
/// (Stammdaten-Vollstaendigkeit). Bewusst keine Grössenheuristik mehr (0,5 % sah wie
/// 50 % aus) — der Aufrufer entscheidet ausdruecklich ueber das Parameter.
/// </summary>
public sealed class AnteilBreiteConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not double wert
            || values[1] is not double breite
            || double.IsNaN(wert) || double.IsNaN(breite))
        {
            return 0.0;
        }

        var istProzent = string.Equals(parameter as string, "prozent", StringComparison.OrdinalIgnoreCase);
        var bruchteil = istProzent ? wert / 100.0 : wert;
        bruchteil = Math.Clamp(bruchteil, 0.0, 1.0);
        return Math.Max(0.0, breite) * bruchteil;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
