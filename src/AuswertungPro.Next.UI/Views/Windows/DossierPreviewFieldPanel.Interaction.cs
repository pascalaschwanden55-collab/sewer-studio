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

    private static void ZeigeWerkzeugeNurAmAktivenFeld(
        FrameworkElement karte,
        UIElement werkzeuge)
        => DossierFieldHighlight.ZeigeWerkzeugeNurAmAktivenFeld(karte, werkzeuge);
}
