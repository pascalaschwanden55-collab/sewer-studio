using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Theme;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Fixwelle 2b (P3): Der Anzeigestil einer einfachen Textzelle — gekuerzt mit
/// Auslassungspunkten statt hart abgeschnitten.
///
/// Die Haltungsliste baut ihren Anzeigestil in <see cref="DataGridStandardTextColumnFactory"/>
/// (dort kommen Namensfettung und Zahlenschrift dazu); die Schachtliste erzeugt ihre
/// Textspalten direkt und holt sich hier denselben einen Setter. So steht die Regel an einer
/// Stelle statt zweimal im Seitencode.
/// </summary>
internal static class NovaTextZellenStil
{
    /// <summary>
    /// Nova-Fixwelle 2b, Runde 2: Rechtes Polster einer Zahlenspalte. Ohne es steht der
    /// rechtsbuendige Wert an der Zellkante und klebt an der Nachbarspalte
    /// („200Kreisprofil", „600Steinzeug").
    /// </summary>
    public static readonly Thickness ZahlenPolster = new(0, 0, 6, 0);

    /// <param name="zahlenspalte">Rechtsbuendige Zahlenspalte: zusaetzlich das rechte Polster.</param>
    public static Style MitAuslassungspunkten(bool zahlenspalte = false)
    {
        var stil = new Style(typeof(TextBlock), ApplicationStyleResolver.FindImplicit(typeof(TextBlock)));
        stil.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        if (zahlenspalte)
            stil.Setters.Add(new Setter(FrameworkElement.MarginProperty, ZahlenPolster));
        return stil;
    }
}
