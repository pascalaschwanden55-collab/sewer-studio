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
/// Balkenbreite aus einem Anteil und der verfuegbaren Breite. Werte &gt; 1 gelten als
/// Prozentzahl (0..100) und werden zuerst durch 100 geteilt; Werte &lt;= 1 sind bereits
/// ein Bruchteil (0..1). So bedient derselbe Konverter sowohl Schadenshaeufigkeit
/// (Bruchteil) als auch Stammdaten-Vollstaendigkeit (Prozent).
/// </summary>
public sealed class AnteilBreiteConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not double anteil
            || values[1] is not double breite
            || double.IsNaN(anteil) || double.IsNaN(breite))
        {
            return 0.0;
        }

        var bruchteil = anteil > 1 ? anteil / 100.0 : anteil;
        bruchteil = Math.Clamp(bruchteil, 0.0, 1.0);
        return Math.Max(0.0, breite) * bruchteil;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
