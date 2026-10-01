using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3, Prototyp v2): Die Zustandsklasse als Marke ("Chip") statt als
/// ganzflaechig eingefaerbte Zelle. Gemeinsam fuer Haltungs- und Schachtliste — beide
/// Datensaetze tragen ihre Werte unter <c>Fields[...]</c>.
///
/// Die Marke ist 34 x 22 (Tokens <c>ZustandsklasseChipBreite/-Hoehe</c>), traegt die
/// unveraenderte Klassenfarbe aus <see cref="ZustandsklasseColorPalette"/> und die dazu
/// lesbare Tinte aus <see cref="ZustandsklasseInkPolicy"/> (mindestens 4,5:1). Ohne gueltige
/// Klasse erscheint ein gestrichelt umrandeter Gedankenstrich mit dem Hinweis
/// "nicht berechnet" — ein Strich ist der getrennte Zustand und darf nie als Z4 gelesen werden.
///
/// Bearbeiten bleibt moeglich: Die Bearbeitungsvorlage ist die Auswahl 0 bis 4 aus
/// <see cref="ZustandsklasseColorPalette.SelectionOptions"/>. Die Auswahl schreibt ueber die
/// TwoWay-Bindung direkt in <c>Fields</c>; Herkunft und Handmarkierung setzt der Zellen-Commit
/// der jeweiligen Seite nach (<c>DataPageCellEditController</c> beziehungsweise
/// <c>SchaechteFieldEditController.ApplyZustandsklasse</c>).
/// </summary>
public static class ZustandsklasseChipColumnFactory
{
    /// <summary>Tinte auf der Klassenfarbe (mindestens 4,5:1). Zustandslos, deshalb einmal.</summary>
    private static readonly ZustandsklasseInkConverter Tinte = new();

    /// <summary>Strichmuster des leeren Chips (Strich, Luecke) in Vielfachen der Strichstaerke.</summary>
    private static readonly DoubleCollection Strichmuster = FrozenStrichmuster();

    public static DataGridTemplateColumn Create(string recordField, string header)
        => new()
        {
            Header = header,
            CellTemplate = Vorlage(Anzeige(recordField)),
            CellEditingTemplate = Vorlage(Auswahl(recordField)),
            Width = DataGridLength.SizeToHeader,
            MinWidth = 90
        };

    /// <summary>
    /// Eine fertige, versiegelte Zellvorlage. Versiegelt wird gleich beim Bauen: WPF versiegelt
    /// eine Vorlage sonst erst beim ersten Anwenden, und vorher laesst sich ihr Baum nicht
    /// erzeugen (das brauchen die Tests).
    /// </summary>
    private static DataTemplate Vorlage(FrameworkElementFactory inhalt)
    {
        var vorlage = new DataTemplate { VisualTree = inhalt };
        vorlage.Seal();
        return vorlage;
    }

    private static FrameworkElementFactory Anzeige(string recordField)
    {
        var wurzel = new FrameworkElementFactory(typeof(Grid));
        wurzel.AppendChild(GefuellteMarke(recordField));
        wurzel.AppendChild(LeereMarke(recordField));
        return wurzel;
    }

    /// <summary>Die farbige Marke mit "Z0" bis "Z4".</summary>
    private static FrameworkElementFactory GefuellteMarke(string recordField)
    {
        var rahmen = new FrameworkElementFactory(typeof(Border));
        Masse(rahmen);
        rahmen.SetResourceReference(Border.CornerRadiusProperty, "RadiusS");
        rahmen.SetBinding(Border.BackgroundProperty, FeldBindung(recordField, ZustandsklasseChipHintergrundConverter.Instance));
        rahmen.SetBinding(
            UIElement.VisibilityProperty,
            FeldBindung(recordField, ZustandsklasseChipSichtbarkeitConverter.Instance, ZustandsklasseChipSichtbarkeitConverter.Gefuellt));

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, FeldBindung(recordField, ZustandsklasseChipTextConverter.Instance));
        text.SetBinding(TextBlock.ForegroundProperty, FeldBindung(recordField, Tinte));
        text.SetResourceReference(TextBlock.FontFamilyProperty, "FontMono");
        text.SetResourceReference(TextBlock.FontSizeProperty, "TextS");
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        Mittig(text);
        rahmen.AppendChild(text);
        return rahmen;
    }

    /// <summary>Der getrennte Zustand "nicht berechnet": gestrichelter Rand, Gedankenstrich.</summary>
    private static FrameworkElementFactory LeereMarke(string recordField)
    {
        var huelle = new FrameworkElementFactory(typeof(Grid));
        Masse(huelle);
        huelle.SetValue(FrameworkElement.ToolTipProperty, "nicht berechnet");
        huelle.SetBinding(
            UIElement.VisibilityProperty,
            FeldBindung(recordField, ZustandsklasseChipSichtbarkeitConverter.Instance, ZustandsklasseChipSichtbarkeitConverter.Leer));

        var rand = new FrameworkElementFactory(typeof(Rectangle));
        rand.SetResourceReference(Shape.StrokeProperty, "MutedBrush");
        rand.SetValue(Shape.StrokeThicknessProperty, 1d);
        rand.SetValue(Shape.StrokeDashArrayProperty, Strichmuster);
        // Die Ecke folgt demselben Radius wie die gefuellte Marke. Rectangle.RadiusX ist ein
        // Double, das Token ein CornerRadius; deshalb wird es hier einmal ausgelesen statt per
        // SetResourceReference gebunden. Ohne laufende Anwendung (Unit-Test) gilt der Wert von
        // RadiusS aus Controls.xaml.
        var radius = System.Windows.Application.Current?.TryFindResource("RadiusS") is CornerRadius ecke ? ecke.TopLeft : 4d;
        rand.SetValue(Rectangle.RadiusXProperty, radius);
        rand.SetValue(Rectangle.RadiusYProperty, radius);
        huelle.AppendChild(rand);

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.TextProperty, ZustandsklasseChipTextConverter.OhneKlasse);
        text.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        text.SetResourceReference(TextBlock.FontSizeProperty, "TextS");
        Mittig(text);
        huelle.AppendChild(text);
        return huelle;
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

    private static Binding FeldBindung(string recordField, IValueConverter converter, object? parameter = null)
        => new($"Fields[{recordField}]")
        {
            Mode = BindingMode.OneWay,
            Converter = converter,
            ConverterParameter = parameter
        };

    private static void Masse(FrameworkElementFactory element)
    {
        element.SetResourceReference(FrameworkElement.WidthProperty, "ZustandsklasseChipBreite");
        element.SetResourceReference(FrameworkElement.HeightProperty, "ZustandsklasseChipHoehe");
        element.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        element.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
    }

    private static void Mittig(FrameworkElementFactory element)
    {
        element.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        element.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
    }

    private static DoubleCollection FrozenStrichmuster()
    {
        var muster = new DoubleCollection { 2d, 2d };
        muster.Freeze();
        return muster;
    }
}
