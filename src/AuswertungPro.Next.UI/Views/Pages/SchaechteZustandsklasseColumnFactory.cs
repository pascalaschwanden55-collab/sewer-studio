using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Die Zustandsklasse-Spalte der Schachtliste: anzeigen als Text, bearbeiten als Auswahl 0 bis 4.
///
/// B6: Vorher war das ein <see cref="DataGridComboBoxColumn"/>. Dessen Anzeigeelement ist kein
/// Textbaustein, sondern ein internes ComboBox-Abkoemmling; ein eigener Stil laesst sich ihm
/// nicht mitgeben (ein Stil mit TargetType TextBlock wirft dort sogar), und der Text bekommt
/// seine Farbe am Ende vom impliziten TextBlock-Stil des Themes — im dunklen Theme weiss auf
/// Gelb. Die Anzeige ist deshalb eine eigene Zellvorlage mit einem Textbaustein, dessen Tinte
/// direkt an der Zelle haengt (schwarz auf der Klassenfarbe). Dasselbe Muster wie
/// <see cref="DataGridComboColumnFactory"/> fuer die uebrigen Auswahlspalten.
/// </summary>
internal static class SchaechteZustandsklasseColumnFactory
{
    public static DataGridTemplateColumn Create(string recordField, string header)
        => new()
        {
            Header = header,
            CellTemplate = new DataTemplate { VisualTree = Anzeige(recordField) },
            CellEditingTemplate = new DataTemplate { VisualTree = Auswahl(recordField) },
            Width = DataGridLength.SizeToHeader,
            MinWidth = 90
        };

    private static FrameworkElementFactory Anzeige(string recordField)
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{recordField}]"));
        text.SetBinding(TextBlock.ForegroundProperty, ZelleTinte());
        text.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        return text;
    }

    private static FrameworkElementFactory Auswahl(string recordField)
    {
        var box = new FrameworkElementFactory(typeof(ComboBox));
        box.SetValue(ItemsControl.ItemsSourceProperty, ZustandsklasseColorPalette.SelectionOptions);
        box.SetBinding(Selector.SelectedItemProperty, new Binding($"Fields[{recordField}]")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        box.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        return box;
    }

    /// <summary>Die Tinte der Zelle; sie traegt die zur Klassenfarbe passende Lesbarkeit.</summary>
    private static Binding ZelleTinte()
        => new("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        };
}
