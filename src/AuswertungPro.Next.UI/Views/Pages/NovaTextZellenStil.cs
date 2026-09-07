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
    public static Style MitAuslassungspunkten()
    {
        var stil = new Style(typeof(TextBlock), ApplicationStyleResolver.FindImplicit(typeof(TextBlock)));
        stil.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        return stil;
    }
}
