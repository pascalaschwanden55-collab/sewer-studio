using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.4, Prototyp v2): Das Schachtprotokoll als Knopf statt als roher
/// Dateipfad — dieselbe Loesung wie die Protokollspalte der Haltungsliste
/// (<see cref="HaltungStatusColumnFactory"/>).
///
/// Es ist eine virtuelle Spalte (<see cref="NovaStatusSpalten.Protokoll"/>): kein Feld, kein
/// Export, keine Layout-Persistenz und ausdruecklich nur lesend. Bearbeitet wird der Pfad
/// weiterhin in "Alle Spalten" und in den Eingabefeldern.
///
/// Der Knopf ruft den bereits vorhandenen Weg der Seite auf
/// (<see cref="SchaechtePage.ProtokollOeffnenCommand"/>, der intern dieselbe Route wie das
/// Kontextmenue nimmt) — es entsteht kein zweiter Dateizugriff.
/// </summary>
public static class SchaechteProtokollColumnFactory
{
    public static DataGridTemplateColumn Create(string header)
        => new()
        {
            Header = header,
            CellTemplate = Vorlage(Zelle()),
            IsReadOnly = true,
            CanUserSort = false,
            Width = DataGridLength.SizeToHeader,
            MinWidth = 78
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

    private static FrameworkElementFactory Zelle()
    {
        var huelle = new FrameworkElementFactory(typeof(Grid));
        huelle.AppendChild(Knopf());
        huelle.AppendChild(Fehlt());
        return huelle;
    }

    private static FrameworkElementFactory Knopf()
    {
        var beschriftung = new FrameworkElementFactory(typeof(TextBlock));
        beschriftung.SetValue(TextBlock.TextProperty, "PDF");
        beschriftung.SetResourceReference(TextBlock.FontSizeProperty, "TextXS");
        beschriftung.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

        var knopf = new FrameworkElementFactory(typeof(Button));
        knopf.SetResourceReference(FrameworkElement.StyleProperty, "IconButton");
        knopf.SetValue(FrameworkElement.WidthProperty, 36d);
        // Ein Glyph oder ein Kuerzel laesst sich nicht vorlesen: Hinweis UND Name werden gesetzt.
        knopf.SetValue(FrameworkElement.ToolTipProperty, "Protokoll öffnen");
        knopf.SetBinding(AutomationProperties.NameProperty, new Binding("Fields[Schachtnummer]")
        {
            Mode = BindingMode.OneWay,
            StringFormat = "Protokoll {0} öffnen"
        });
        knopf.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(SchaechtePage.ProtokollOeffnenCommand))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(SchaechtePage), 1)
        });
        knopf.SetBinding(ButtonBase.CommandParameterProperty, new Binding("."));
        knopf.SetBinding(
            UIElement.VisibilityProperty,
            SchachtProtokollSichtbarkeitConverter.Bindung(SchachtProtokollSichtbarkeitConverter.Protokoll));
        knopf.AppendChild(beschriftung);
        return knopf;
    }

    /// <summary>Kein Protokoll: ein Gedankenstrich mit Hinweis — nie ein toter Knopf.</summary>
    private static FrameworkElementFactory Fehlt()
    {
        var strich = new FrameworkElementFactory(typeof(TextBlock));
        strich.SetValue(TextBlock.TextProperty, "–");
        strich.SetValue(FrameworkElement.ToolTipProperty, "kein Protokoll");
        strich.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        strich.SetResourceReference(TextBlock.FontSizeProperty, "TextS");
        strich.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        strich.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        strich.SetBinding(
            UIElement.VisibilityProperty,
            SchachtProtokollSichtbarkeitConverter.Bindung(SchachtProtokollSichtbarkeitConverter.KeinProtokoll));
        return strich;
    }
}
