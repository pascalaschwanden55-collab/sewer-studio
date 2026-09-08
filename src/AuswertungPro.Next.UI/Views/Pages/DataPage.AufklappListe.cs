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
                NovaSucheLeiste, HaltungsansichtToggle, AnsichtListeMenu, AnsichtTabelleMenu, UndockButton),
            () => (DataContext as DataPageViewModel)?.Settings,
            () => (DataContext as DataPageViewModel)?.Settings.Save(),
            (uebersicht, felder) => SetNovaWorkspaceVisible(uebersicht, felder));
        WendeAnsichtAn();
    }

    /// <summary>Ansicht anwenden und einen noch offenen Sprung von aussen nachholen.</summary>
    private void WendeAnsichtAn()
    {
        _ansicht?.WendeAn();
        _ansicht?.ZeigeHaltung((DataContext as DataPageViewModel)?.NimmAnzeigeAuftrag());
    }

    private void HaltungsansichtToggle_Changed(object sender, RoutedEventArgs e) => WendeAnsichtAn();

    private void AnsichtMenu_Click(object sender, RoutedEventArgs e) => _ansicht?.Waehle(sender);

    /// <summary>Auswahl gewechselt: offenes Formular pruefen und die Zeile in Sicht scrollen.</summary>
    private void AktualisiereAufklappListe()
    {
        _aufklappListe?.AktualisiereFormular();
        _ansicht?.FolgeAuswahl();
    }

    /// <summary>Sprung aus Dossier, Karte oder Suche: die Haltung in der Liste aufklappen.</summary>
    private void ZeigeHaltungInListe(HaltungRecord record) => _ansicht?.ZeigeHaltung(record);
}
