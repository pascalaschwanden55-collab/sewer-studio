using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>Nova, Aufklapp-Liste: duenne Anbindung. Formularaufbau und Ansichtswechsel liegen in
/// <see cref="DataPageAufklappListeController"/> und <see cref="DataPageAnsichtUmschalter"/>.</summary>
public partial class DataPage
{
    private DataPageAufklappListeController? _aufklappListe;
    private DataPageAnsichtUmschalter? _ansicht;

    private void VerdrahteAufklappListe()
    {
        _aufklappListe = new DataPageAufklappListeController(
            AufklappListe, () => DataContext as DataPageViewModel, BuildHaltungRecordDetailsForAnsicht);
        _aufklappListe.Verdrahte();
        _ansicht = new DataPageAnsichtUmschalter(
            new DataPageAnsichtUmschalter.Elemente(
                Grid, HaltungsansichtView, AufklappListe, ColumnViewChips, AlteSucheLeiste,
                NovaSucheLeiste, HaltungsansichtToggle, AnsichtListeMenu, AnsichtTabelleMenu,
                UndockButton, (System.Windows.Controls.ContextMenu)FindResource("HaltungZeilenMenue")),
            () => (DataContext as DataPageViewModel)?.Settings,
            () => (DataContext as DataPageViewModel)?.Settings.Save(),
            (uebersicht, felder) => SetNovaWorkspaceVisible(uebersicht, felder),
            (feld, aktuell, eingabe) => _aufklappListe?.MeldeKonflikt(feld, aktuell, eingabe),
            (feld, aktuell, eingabe) => _novaWorkspace?.MeldeKonflikt(feld, aktuell, eingabe));
        _docking = new DataPageDockingHost(
            new DataPageDockingHost.Elemente(
                GridHost, Grid, HaltungsansichtView, UndockedPlaceholder, UndockButton, HaltungsansichtToggle),
            () => DataContext as DataPageViewModel,
            (text, titel) => (DataContext as DataPageViewModel)?.Dialogs.Warn(text, titel),
            WendeAnsichtAn);
        WendeAnsichtAn();
    }

    /// <summary>Ansicht anwenden, Formulare nachziehen, offenen Sprung von aussen nachholen.</summary>
    private void WendeAnsichtAn()
    {
        _ansicht?.WendeAn();
        AktualisiereFormulare();
        _ansicht?.ZeigeHaltung((DataContext as DataPageViewModel)?.NimmAnzeigeAuftrag());
    }

    /// <summary>
    /// Genau ein Formular je Datensatz: In der Liste steht es in der aufgeklappten Zeile, in der
    /// Tabelle in der Schublade. Das jeweils unsichtbare wird entsorgt, nicht nur ausgeblendet.
    /// </summary>
    private void AktualisiereFormulare()
    {
        if (_ansicht?.ListeSichtbar != true)
            AufklappListe.KlappeZu();
        _aufklappListe?.AktualisiereFormular();
        AktualisiereFelderDrawer();
    }

    private void HaltungsansichtToggle_Changed(object sender, RoutedEventArgs e) => WendeAnsichtAn();

    /// <summary>
    /// Ansicht aus dem Menue waehlen. <c>Waehle</c> wendet sie selbst an; nachzuziehen sind nur
    /// die Formulare — sonst bliebe das der vorigen Ansicht stehen.
    /// </summary>
    private void AnsichtMenu_Click(object sender, RoutedEventArgs e)
    {
        _ansicht?.Waehle(sender);
        AktualisiereFormulare();
    }

    /// <summary>Auswahl gewechselt: offenes Formular pruefen und die Zeile in Sicht scrollen.</summary>
    private void AktualisiereAufklappListe()
    {
        _aufklappListe?.AktualisiereFormular();
        _ansicht?.FolgeAuswahl();
    }

    /// <summary>Sprung aus Dossier, Karte oder Suche: die Haltung in der Liste aufklappen.</summary>
    private void ZeigeHaltungInListe(HaltungRecord record) => _ansicht?.ZeigeHaltung(record);

    /// <summary>
    /// Abo des Sprungs von aussen, symmetrisch: <c>Unloaded</c> meldet ab, <c>Loaded</c> meldet
    /// wieder an. Wird dieselbe Seite erneut geladen, waere der Sprung sonst tot — und das faellt
    /// niemandem auf, weil die Haltung trotzdem ausgewaehlt wird, nur eben nicht aufklappt.
    /// Mehrfach sicher aufrufbar: Es wird immer zuerst abgemeldet.
    /// </summary>
    private void VerbindeAnzeigeAuftrag(bool an)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        vm.HaltungAnzeigen -= ZeigeHaltungInListe;
        if (an)
            vm.HaltungAnzeigen += ZeigeHaltungInListe;
    }
}
