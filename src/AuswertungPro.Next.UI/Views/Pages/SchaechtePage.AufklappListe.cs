using System.ComponentModel;
using System.Windows;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova, Aufklapp-Liste (Task 6): duenne Anbindung. Formularaufbau und Ansichtswechsel liegen
/// in <see cref="SchaechteAufklappListeController"/> und <see cref="SchaechteAnsichtUmschalter"/>
/// — nach demselben Muster wie <c>DataPage.AufklappListe.cs</c>.
/// </summary>
public partial class SchaechtePage
{
    private SchaechteAufklappListeController? _aufklappListe;
    private SchaechteAnsichtUmschalter? _ansichtSchacht;
    private SchaechtePageViewModel? _vmMitAufklappAbo;

    private void VerdrahteAufklappListe()
    {
        _aufklappListe = new SchaechteAufklappListeController(
            AufklappListe, () => DataContext as SchaechtePageViewModel, BuildRecordDetailsForAnsicht);
        _aufklappListe.Verdrahte();
        _ansichtSchacht = new SchaechteAnsichtUmschalter(
            new SchaechteAnsichtUmschalter.Elemente(
                Grid, SchachtansichtView, AufklappListe, ColumnViewChips,
                SchachtansichtToggle, AnsichtListeMenu, AnsichtTabelleMenu),
            () => (DataContext as SchaechtePageViewModel)?.Settings,
            () => (DataContext as SchaechtePageViewModel)?.Settings.Save(),
            (uebersicht, felder) => _novaWorkspace?.SetzeSichtbar(uebersicht, felder));
        WendeSchachtAnsichtAn();
    }

    /// <summary>Ansicht anwenden und die Formulare nachziehen.</summary>
    private void WendeSchachtAnsichtAn()
    {
        _ansichtSchacht?.WendeAn();
        AktualisiereSchachtFormulare();
    }

    /// <summary>
    /// Genau ein Formular je Datensatz: In der Liste steht es in der aufgeklappten Zeile, in
    /// der Tabelle in der Eingabefelder-Schublade. Das jeweils unsichtbare wird entsorgt statt
    /// nur ausgeblendet.
    /// </summary>
    private void AktualisiereSchachtFormulare()
    {
        if (_ansichtSchacht?.ListeSichtbar != true)
            AufklappListe.KlappeZu();
        _aufklappListe?.AktualisiereFormular();
        AktualisiereFelderDrawer();
    }

    /// <summary>
    /// Ansicht aus dem Menue waehlen. <c>Waehle</c> wendet sie selbst an; nachzuziehen sind nur
    /// die Formulare — sonst bliebe das der vorigen Ansicht stehen.
    /// </summary>
    private void AnsichtMenu_Click(object sender, RoutedEventArgs e)
    {
        _ansichtSchacht?.Waehle(sender);
        AktualisiereSchachtFormulare();
    }

    /// <summary>Auswahl gewechselt: offenes Formular pruefen und die Zeile in Sicht scrollen.</summary>
    private void AktualisiereAufklappListe()
    {
        _aufklappListe?.AktualisiereFormular();
        _ansichtSchacht?.FolgeAuswahl();
    }

    /// <summary>
    /// Abo auf <c>Selected</c>, symmetrisch: bei jedem DataContext-Wechsel wird zuerst
    /// abgemeldet und danach — falls ein ViewModel da ist — neu angemeldet. Ohne das bliebe bei
    /// einem erneuten Laden derselben Seite ein Sprung von aussen (z. B. Dossier) folgenlos:
    /// Die Auswahl aendert sich zwar, das Formular der Liste zieht aber nicht nach.
    /// </summary>
    private void VerdrahteAufklappAbo(SchaechtePageViewModel? vm)
    {
        if (_vmMitAufklappAbo is not null)
            _vmMitAufklappAbo.PropertyChanged -= AufklappViewModel_PropertyChanged;

        _vmMitAufklappAbo = vm;
        if (vm is not null)
            vm.PropertyChanged += AufklappViewModel_PropertyChanged;
    }

    private void AufklappViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SchaechtePageViewModel.Selected))
            AktualisiereAufklappListe();
    }
}
