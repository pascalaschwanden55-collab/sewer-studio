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
        AktualisiereFelderDrawer();
        // Standardansicht: Nova-Arbeitsflaeche; die bisherige Haltungsansicht bleibt ueber den Toggle
        // erreichbar und wird Standard, wenn die Einstellung aus ist.
        HaltungsansichtToggle.IsChecked = !vm.Settings.ShowHaltungenNovaLayout;
        WendeAnsichtAn();
    }

    private void AktualisiereFelderDrawer() => _novaWorkspace?.AktualisiereFelderDrawer();

    private void ApplyDrawerHeight() => _novaWorkspace?.ApplyDrawerHeight();

    // Vor VerdrahteNovaWorkspace (waehrend InitializeComponent) gilt der XAML-Grundzustand.
    private void SetNovaWorkspaceVisible(bool uebersicht, bool eingabefelder)
        => _novaWorkspace?.SetzeSichtbar(uebersicht, eingabefelder);

    private void MeldeFormularKonflikt(string fieldName, string aktuellerWert, string eingabe)
        => _novaWorkspace?.MeldeKonflikt(fieldName, aktuellerWert, eingabe);
}
