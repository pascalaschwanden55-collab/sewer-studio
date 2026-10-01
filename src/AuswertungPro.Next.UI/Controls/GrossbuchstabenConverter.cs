using System;
using System.Globalization;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3): Wandelt einen String in Grossbuchstaben (andere Inhalte
/// bleiben unveraendert, damit ein nicht-textueller Wert nicht kaputtgeht). Registriert app-weit
/// als XAML-Ressource "Grossbuchstaben" (<c>App.xaml</c>) fuer echte Text-Bindings;
/// <see cref="Anwenden"/> ist dieselbe Regel als reine C#-Methode.
///
/// Der Tabellenkopf des DataGrid (<c>Theme.xaml</c>/<c>ThemeLight.xaml</c>) bindet diesen
/// Konverter NICHT im Kopf-Template: <c>PageTitleUnderlineTests</c> laedt diese beiden Dateien
/// roh per <c>XamlReader.Load</c> aus dem Testprojekt (ohne <c>App.InitializeComponent()</c>),
/// und dieser Ladeweg parst die GESAMTE Datei sofort — auch Inhalt in ControlTemplate.Resources
/// oder einer verschachtelten DataTemplate.Resources (anders als beim kompilierten BAML-Weg
/// ueber <c>App.InitializeComponent()</c>, der Vorlageninhalt erst beim tatsaechlichen Anwenden
/// laedt). Jedes Objektelement fuer eine eigene Klasse scheitert dort mit "unbekannter Typ";
/// <c>DynamicResource</c> ist ausserdem fuer <see cref="Binding.Converter"/> keine Option
/// (gemessen: "DynamicResourceExtension kann nur fuer eine DependencyProperty eines
/// DependencyObject festgelegt werden", Binding ist keine DependencyObject).
///
/// Die Spaltenkoepfe von Haltungen (<c>DataGridColumnFactory.Create</c>) und Schaechten
/// (<c>SchaechtePage.RebuildColumns</c>, an der einen Stelle, wo <c>column.Header</c> endgueltig
/// gesetzt wird) rufen deshalb <see cref="Anwenden"/> direkt beim Erzeugen der Spalte auf; das
/// Kopf-Template bindet den bereits fertigen String unveraendert.
/// <c>SchaechteColumnPolicy.GetDisplayHeader</c> selbst bleibt unveraendert, weil
/// <c>SchaechteRecordDetailsBuilder</c> denselben Text auch als normale Feldbeschriftung im
/// Formular verwendet.
/// </summary>
public sealed class GrossbuchstabenConverter : IValueConverter
{
    /// <summary>Reine Transformationsregel, ohne WPF testbar.</summary>
    public static string? Anwenden(string? wert) => wert?.ToUpperInvariant();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string text ? Anwenden(text) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
