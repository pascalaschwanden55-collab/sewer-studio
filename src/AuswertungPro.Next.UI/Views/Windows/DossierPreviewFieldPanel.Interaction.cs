using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Verdrahtung der Eingabeseite mit ihren zwei Verhaltensregeln.
///
/// Die Regeln selbst stehen in <see cref="DossierFieldSectionAccordion"/> und
/// <see cref="DossierFieldHighlight"/> — dort sind sie ohne Fenster, Vorlage
/// und Dossier prüfbar.
/// </summary>
internal sealed partial class DossierPreviewFieldPanel
{
    private readonly DossierFieldSectionAccordion _ordnung = new();

    private void MerkeAbschnitt(Expander abschnitt) => _ordnung.Merke(abschnitt);

    private void NurDiesenAbschnitt(Expander offen) => _ordnung.OeffneNur(offen);

    private void OeffneNurDenErsten() => _ordnung.OeffneNurDenErsten();

    private void LeereAbschnitte() => _ordnung.Leere();

    private static void LasseAufblinken(FrameworkElement stelle)
        => DossierFieldHighlight.LasseAufblinken(stelle);

    private static void AktiviereEingabe(FrameworkElement stelle)
        => DossierFieldHighlight.AktiviereEingabe(stelle);

    private static void ZeigeWerkzeugeNurAmAktivenFeld(
        FrameworkElement karte,
        UIElement werkzeuge)
        => DossierFieldHighlight.ZeigeWerkzeugeNurAmAktivenFeld(karte, werkzeuge);

    // ── Nur das angeklickte Feld zeigen ───────────────────────────────────

    /// <summary>Die Kopfzeile mit dem Rueckweg aus dem Fokus.</summary>
    private Border? _fokuszeile;

    /// <summary>
    /// Legt die Kopfzeile an, die aus dem Fokus zurueckfuehrt. Sie liegt
    /// ausserhalb der Abschnitte und wird deshalb vom Fokus nie ausgeblendet.
    /// </summary>
    private void BaueFokuszeile()
    {
        var zurueck = new Button
        {
            Content = "Alle Felder dieser Seite zeigen",
            Padding = new Thickness(9, 3, 9, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = "Ein leeres Feld hat im Blatt keinen Text zum Anklicken — "
                + "hierueber kommt man trotzdem hin."
        };

        zurueck.Click += (_, _) => ZeigeAlleFelder();

        _fokuszeile = new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Child = zurueck,
            Visibility = Visibility.Collapsed
        };

        _wirt.Children.Add(_fokuszeile);
    }

    /// <summary>Rechts bleibt nur die angeklickte Stelle stehen.</summary>
    private void ZeigeNurDieseStelle(FrameworkElement stelle)
    {
        DossierFieldFocus.ZeigeNur(Fokusfaehige(), stelle);

        if (_fokuszeile is not null)
            _fokuszeile.Visibility = Visibility.Visible;
    }

    /// <summary>Zurueck zur ganzen Seite.</summary>
    private void ZeigeAlleFelder()
    {
        DossierFieldFocus.ZeigeAlles(Fokusfaehige());

        if (_fokuszeile is not null)
            _fokuszeile.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Alles, was der Fokus ein- und ausblenden darf: die Abschnitte und die
    /// gemerkten Eingabestellen. Die Kopfzeile gehoert bewusst nicht dazu.
    /// </summary>
    private IEnumerable<FrameworkElement> Fokusfaehige()
        => _ordnung.Abschnitte
            .Cast<FrameworkElement>()
            .Concat(_feldStellen.Values);
}
