using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3/4.4): Die gemeinsamen Bausteine der nur lesenden Statusspalten
/// von Haltungs- und Schachtliste.
///
/// Beide Listen zeigen dieselbe Zelle: entweder einen kleinen Knopf, der eine bereits
/// vorhandene Aktion der Seite mit dem Datensatz der Zeile aufruft, oder — wenn nichts
/// hinterlegt ist — einen Gedankenstrich mit Hinweis.
///
/// Nova-Fixwelle 2b (F1): Einen Knopf gibt es genau dann, wenn ein Pfad hinterlegt ist. Ob die
/// Datei wirklich noch da ist, sagt erst der Oeffner — die Zelle prueft das nicht, weil sie
/// sonst je Zeile das Dateisystem befragen muesste.
///
/// Was sich je Liste unterscheidet, kommt als Bindung herein: der Befehl, der vorlesbare Name
/// und die Sichtbarkeit. Der Rest (Stil, Hinweis, Parameter, Ausrichtung) ist hier einmal
/// festgelegt.
/// </summary>
internal static class StatusZellenBausteine
{
    /// <summary>
    /// Nova-Fixwelle 2b (P2): Ein Knopf IN der Tabelle ist 24 px hoch, nicht 28 wie in einer
    /// Werkzeugleiste. Gemessen war genau er der hoechste Zellinhalt und bestimmte damit die
    /// Zeilenhoehe der ganzen Tabelle — die Zeilenhoehe aus <c>RowHeightCompact</c> ist nur
    /// eine Untergrenze und konnte dagegen nichts ausrichten.
    /// </summary>
    private const double KnopfHoeheInDerZelle = 24;

    /// <summary>
    /// Ein Knopf mit ToolTip UND vorlesbarem Namen: Ein Glyph oder ein Kuerzel wie "PDF" laesst
    /// sich nicht vorlesen. Der Parameter ist immer der Datensatz der Zeile.
    /// </summary>
    internal static FrameworkElementFactory Aktionsknopf(
        BindingBase befehl,
        BindingBase name,
        string hinweis,
        BindingBase sichtbarkeit,
        FrameworkElementFactory inhalt,
        double? breite = null)
    {
        var knopf = new FrameworkElementFactory(typeof(Button));
        knopf.SetResourceReference(FrameworkElement.StyleProperty, "IconButton");
        knopf.SetValue(FrameworkElement.HeightProperty, KnopfHoeheInDerZelle);
        knopf.SetValue(FrameworkElement.MinHeightProperty, KnopfHoeheInDerZelle);
        knopf.SetValue(FrameworkElement.ToolTipProperty, hinweis);
        knopf.SetBinding(AutomationProperties.NameProperty, name);
        knopf.SetBinding(ButtonBase.CommandProperty, befehl);
        knopf.SetBinding(ButtonBase.CommandParameterProperty, new Binding("."));
        knopf.SetBinding(UIElement.VisibilityProperty, sichtbarkeit);
        if (breite is double b)
            knopf.SetValue(FrameworkElement.WidthProperty, b);
        knopf.AppendChild(inhalt);
        return knopf;
    }

    /// <summary>Kein Video beziehungsweise kein Protokoll: ein Gedankenstrich mit Hinweis.</summary>
    internal static FrameworkElementFactory Fehlt(BindingBase sichtbarkeit, string hinweis)
    {
        var strich = new FrameworkElementFactory(typeof(TextBlock));
        strich.SetValue(TextBlock.TextProperty, "–");
        strich.SetValue(FrameworkElement.ToolTipProperty, hinweis);
        strich.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        strich.SetResourceReference(TextBlock.FontSizeProperty, "TextS");
        strich.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        strich.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        strich.SetBinding(UIElement.VisibilityProperty, sichtbarkeit);
        return strich;
    }

    /// <summary>Die kleine, fette Kurzbeschriftung eines Knopfs ("PDF").</summary>
    internal static FrameworkElementFactory Kuerzel(string text)
    {
        var beschriftung = new FrameworkElementFactory(typeof(TextBlock));
        beschriftung.SetValue(TextBlock.TextProperty, text);
        beschriftung.SetResourceReference(TextBlock.FontSizeProperty, "TextXS");
        beschriftung.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        return beschriftung;
    }
}
