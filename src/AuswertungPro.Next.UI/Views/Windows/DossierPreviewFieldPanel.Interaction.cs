using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Verdrahtung der Eingabeseite mit ihrem Verhalten.
///
/// Die Regeln selbst stehen in <see cref="DossierFieldHighlight"/> und
/// <see cref="DossierFieldFocus"/> — dort sind sie ohne Fenster, Vorlage und
/// Dossier prüfbar.
/// </summary>
internal sealed partial class DossierPreviewFieldPanel
{
    /// <summary>
    /// Die Abschnitte der Eingabeseite, in ihrer Reihenfolge. Sie stehen alle
    /// offen; ein- und ausgeblendet wird nur noch ueber den Klick im Blatt.
    /// </summary>
    private readonly List<FrameworkElement> _abschnitte = new();

    private void MerkeAbschnitt(FrameworkElement abschnitt) => _abschnitte.Add(abschnitt);

    private void LeereAbschnitte() => _abschnitte.Clear();

    private static void LasseAufblinken(FrameworkElement stelle)
        => DossierFieldHighlight.LasseAufblinken(stelle);

    private static void AktiviereEingabe(FrameworkElement stelle)
        => DossierFieldHighlight.AktiviereEingabe(stelle);

    private static void ZeigeWerkzeugeNurAmAktivenFeld(
        FrameworkElement karte,
        UIElement werkzeuge)
        => DossierFieldHighlight.ZeigeWerkzeugeNurAmAktivenFeld(karte, werkzeuge);

    // ── Nur das angeklickte Feld zeigen ───────────────────────────────────

    /// <summary>Die stets sichtbare Kopfzeile fuer Textaktionen und Rueckweg.</summary>
    private Border? _fokuszeile;

    /// <summary>Nur dieser Knopf erscheint waehrend des Blatt-Fokus.</summary>
    private Button? _alleFelderKnopf;

    /// <summary>
    /// Legt die Kopfzeile mit Textverlauf und Rueckweg an. Sie liegt ausserhalb
    /// der Abschnitte und wird deshalb vom Fokus nie ausgeblendet.
    /// </summary>
    private void BaueFokuszeile()
    {
        if (_fokuszeile is not null)
        {
            _wirt.Children.Add(_fokuszeile);
            return;
        }

        _alleFelderKnopf = new Button
        {
            Content = "Alle Felder anzeigen",
            Padding = new Thickness(9, 3, 9, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed,
            ToolTip = "Zeigt wieder alle Eingabefelder der aktuellen Seite."
        };

        _alleFelderKnopf.Click += (_, _) => ZeigeAlleFelder();

        var leiste = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(_alleFelderKnopf, Dock.Left);
        DockPanel.SetDock(_textUndo.View, Dock.Right);
        leiste.Children.Add(_alleFelderKnopf);
        leiste.Children.Add(_textUndo.View);

        _fokuszeile = new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Child = leiste
        };

        _wirt.Children.Add(_fokuszeile);
    }

    /// <summary>Rechts bleibt nur die angeklickte Stelle stehen.</summary>
    private void ZeigeNurDieseStelle(FrameworkElement stelle)
    {
        DossierFieldFocus.ZeigeNur(Fokusfaehige(), stelle);

        if (_alleFelderKnopf is not null)
            _alleFelderKnopf.Visibility = Visibility.Visible;
    }

    /// <summary>Zurueck zur ganzen Seite.</summary>
    private void ZeigeAlleFelder()
    {
        DossierFieldFocus.ZeigeAlles(Fokusfaehige());

        if (_alleFelderKnopf is not null)
            _alleFelderKnopf.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Alles, was der Fokus ein- und ausblenden darf: die Abschnitte und die
    /// gemerkten Eingabestellen. Die Kopfzeile gehoert bewusst nicht dazu.
    /// </summary>
    private IEnumerable<FrameworkElement> Fokusfaehige()
        => _abschnitte.Concat(_feldStellen.Values);
}
