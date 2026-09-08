using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 1: duenne Anbindung der Arbeitsflaeche (Liste, Uebersicht rechts, Eingabefelder
/// unten). Die Logik liegt in <see cref="DataPageNovaWorkspaceController"/>; hier werden nur
/// die benannten XAML-Elemente uebergeben.
/// </summary>
public partial class DataPage
{
    private DataPageNovaWorkspaceController? _novaWorkspace;

    /// <summary>Einmalige Verdrahtung im Konstruktor, unabhaengig vom ViewModel.</summary>
    private void VerdrahteNovaWorkspace()
    {
        _novaWorkspace = new DataPageNovaWorkspaceController(
            new DataPageNovaWorkspaceController.Elemente(
                GridHost, FilterChips, DrawerSplitterRow, DrawerRow, SideSplitterCol, SideCol,
                SideSplitter, DrawerSplitter, Uebersicht, FelderDrawer),
            () => DataContext as DataPageViewModel,
            BuildHaltungRecordDetailsForAnsicht,
            record => RouteHaltungsansichtAction("beobachtungen", record));
        _novaWorkspace.Verdrahte();
    }

    /// <summary>Verbindet Uebersicht und Eingabefelder mit dem ViewModel und waehlt die Standardansicht.</summary>
    private void InitNovaWorkspace(DataPageViewModel vm)
    {
        _novaWorkspace?.VerbindeKatalog();
        AktualisiereFelderDrawer();
        // Standardansicht: Nova-Arbeitsflaeche; die bisherige Haltungsansicht bleibt ueber den Toggle
        // erreichbar und wird Standard, wenn die Einstellung aus ist.
        HaltungsansichtToggle.IsChecked = !vm.Settings.ShowHaltungenNovaLayout;
        WendeAnsichtAn();
    }

    /// <summary>
    /// Die Eingabefelder unten gehoeren zur Tabelle. Zeigt die Seite die Aufklapp-Liste, wird
    /// die Schublade geleert und ihr Live-Abgleich entsorgt: ein Formular je Datensatz.
    /// </summary>
    private void AktualisiereFelderDrawer()
    {
        if (_ansicht?.ListeSichtbar == true)
            _novaWorkspace?.LeereFelderDrawer();
        else
            _novaWorkspace?.AktualisiereFelderDrawer();
    }

    private void ApplyDrawerHeight() => _novaWorkspace?.ApplyDrawerHeight();

    // Vor VerdrahteNovaWorkspace (waehrend InitializeComponent) gilt der XAML-Grundzustand.
    private void SetNovaWorkspaceVisible(bool uebersicht, bool eingabefelder)
        => _novaWorkspace?.SetzeSichtbar(uebersicht, eingabefelder);

    /// <summary>W01: Die Weiche zwischen Liste und Schublade liegt im Ansichtsumschalter.</summary>
    private void MeldeFormularKonflikt(string fieldName, string aktuellerWert, string eingabe)
        => _ansicht?.MeldeKonflikt(fieldName, aktuellerWert, eingabe);
}
