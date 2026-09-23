using System.Globalization;
using System.Windows.Data;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Auswahlspalten der Schachttabelle ohne eigene verwaltete Liste — Status, Sanierungsbedarf, Nutzungsart,
/// Material, Schachtform … (Audit A15, 23.09.2026). Sie zeigten einen vorhandenen Wert leer; ein Wert, der
/// nicht in der Liste steht, wurde nie angeboten. Und schon das Verlassen der Zelle schrieb den Wert mit
/// Handmarke neu — ein Kanalfirma-Wert waere so ohne Haekchen ins WebGIS gegangen.
/// </summary>
internal sealed class SchachtTabellenAuswahl : IMultiValueConverter
{
    /// <summary>Die Liste; ein gespeicherter Wert ausserhalb der Liste wird zeichengenau hinten angehaengt.</summary>
    internal static IReadOnlyList<string> MitAltwert(IEnumerable<string>? liste, string? aktuell)
    {
        var werte = (liste ?? Array.Empty<string>()).ToList();
        var wert = aktuell ?? string.Empty;
        if (wert.Trim().Length > 0 && !werte.Contains(wert, StringComparer.Ordinal))
            werte.Add(wert);
        return werte;
    }

    /// <summary>Geschrieben wird nur, wenn sich der Wert wirklich aendert — sonst stempelt blosses Verlassen eine Handmarke.</summary>
    internal static bool IstAenderung(string? bisher, string? neu)
        => !string.Equals((bisher ?? string.Empty).Trim(), (neu ?? string.Empty).Trim(), StringComparison.Ordinal);

    /// <summary>Auswahl je Zeile: die Liste des ViewModels plus der gespeicherte Wert dieser Zeile.</summary>
    internal static BindingBase FuerSpalte(string feld, string itemsSourcePath)
        => new MultiBinding
        {
            Converter = new SchachtTabellenAuswahl(),
            Bindings =
            {
                new Binding($"DataContext.{itemsSourcePath}")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1),
                },
                new Binding($"Fields[{feld}]"),
            },
        };

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => MitAltwert(values.ElementAtOrDefault(0) as IEnumerable<string>, values.ElementAtOrDefault(1) as string);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
