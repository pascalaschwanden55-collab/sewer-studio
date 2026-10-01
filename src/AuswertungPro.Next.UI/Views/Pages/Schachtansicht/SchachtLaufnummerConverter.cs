using System.Globalization;
using System.Windows.Data;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

/// <summary>Liest dieselbe Nummernspalte wie die bestehende Schacht-Durchnummerierung.</summary>
public sealed class SchachtLaufnummerConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not { Length: > 0 } || values[0] is not SchachtRecord record)
            return string.Empty;

        var feld = SchaechteFieldLogic.ResolveNrColumnName([], [record]);
        return feld is null ? string.Empty : record.GetFieldValue(feld);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
