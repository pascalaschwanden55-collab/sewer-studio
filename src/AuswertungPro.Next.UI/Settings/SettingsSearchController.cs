using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Settings;

/// <summary>
/// Filtert die Einstellungsgruppen und springt bei Bedarf zum ersten Reiter mit Treffer.
/// </summary>
public sealed class SettingsSearchController(TabControl reiter)
{
    private const double AbgedunkeltOpacity = 0.45;

    /// <summary>Liefert die Zahl der sichtbaren Gruppen.</summary>
    public int Anwenden(string suche)
    {
        var sichtbarGesamt = 0;
        TabItem? ersterMitTreffer = null;

        foreach (var tab in reiter.Items.OfType<TabItem>())
        {
            var sichtbarImReiter = 0;
            foreach (var gruppe in Gruppen(tab))
            {
                var passt = SettingsSearchMatcher.Passt(suche, Texte(gruppe));
                gruppe.Visibility = passt ? Visibility.Visible : Visibility.Collapsed;
                if (passt)
                    sichtbarImReiter++;
            }

            tab.Opacity = sichtbarImReiter == 0 && !string.IsNullOrWhiteSpace(suche)
                ? AbgedunkeltOpacity
                : 1.0;
            if (sichtbarImReiter > 0)
                ersterMitTreffer ??= tab;
            sichtbarGesamt += sichtbarImReiter;
        }

        var aktuell = reiter.SelectedItem as TabItem;
        if (!string.IsNullOrWhiteSpace(suche)
            && ersterMitTreffer is not null
            && (aktuell is null || aktuell.Opacity < 1.0))
        {
            reiter.SelectedItem = ersterMitTreffer;
        }

        return sichtbarGesamt;
    }

    private static IEnumerable<GroupBox> Gruppen(TabItem tab)
        => Nachfahren(tab.Content as DependencyObject).OfType<GroupBox>();

    /// <summary>Liest Ueberschrift, Texte, Beschriftungen und Tooltips einer Gruppe.</summary>
    private static IEnumerable<string> Texte(GroupBox gruppe)
    {
        if (gruppe.Header is string kopf)
            yield return kopf;

        foreach (var element in Nachfahren(gruppe))
        {
            switch (element)
            {
                case TextBlock text:
                    yield return text.Text;
                    break;
                case ContentControl { Content: string beschriftung }:
                    yield return beschriftung;
                    break;
            }

            if (element is FrameworkElement { ToolTip: string tooltip })
                yield return tooltip;
        }
    }

    private static IEnumerable<DependencyObject> Nachfahren(DependencyObject? wurzel)
    {
        if (wurzel is null)
            yield break;

        foreach (var kind in LogicalTreeHelper.GetChildren(wurzel).OfType<DependencyObject>())
        {
            yield return kind;
            foreach (var enkel in Nachfahren(kind))
                yield return enkel;
        }
    }
}
