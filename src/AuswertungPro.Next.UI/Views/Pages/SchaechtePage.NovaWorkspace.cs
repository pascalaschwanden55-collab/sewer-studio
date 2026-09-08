using System.Windows;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2: duenne Anbindung der Arbeitsflaeche (Liste, Schachtansicht rechts,
/// Eingabefelder unten). Die Logik liegt in <see cref="SchaechteNovaWorkspaceController"/>; hier
/// werden nur die benannten XAML-Elemente uebergeben und der Umschalter zur alten Schachtansicht
/// verdrahtet.
/// </summary>
public partial class SchaechtePage
{
    private SchaechteNovaWorkspaceController? _novaWorkspace;

    /// <summary>Einmalige Verdrahtung im Konstruktor, unabhaengig vom ViewModel.</summary>
    private void VerdrahteNovaWorkspace()
    {
        _novaWorkspace = new SchaechteNovaWorkspaceController(
            new SchaechteNovaWorkspaceController.Elemente(
                GridHost, DrawerSplitterRow, DrawerRow, SideSplitterCol, SideCol,
                SideSplitter, DrawerSplitter, Uebersicht, FelderDrawer),
            () => DataContext as SchaechtePageViewModel,
            BuildRecordDetailsForAnsicht,
            record => RouteSchachtansichtAction("openpdf", record));
        _novaWorkspace.Verdrahte();
    }

    /// <summary>Verbindet Uebersicht und Eingabefelder mit dem ViewModel und waehlt die Standardansicht.</summary>
    private void InitNovaWorkspace(SchaechtePageViewModel vm)
    {
        _novaWorkspace?.VerbindeKatalog();
        AktualisiereFelderDrawer();
        // Standardansicht: Nova-Arbeitsflaeche; die alte Schachtansicht bleibt ueber den Toggle
        // erreichbar und wird Standard, wenn die Einstellung aus ist.
        SchachtansichtToggle.IsChecked = !vm.Settings.ShowSchaechteNovaLayout;
        WendeSchachtAnsichtAn();
    }

    private void AktualisiereFelderDrawer() => _novaWorkspace?.AktualisiereFelderDrawer();

    private void ApplyDrawerHeight() => _novaWorkspace?.ApplyDrawerHeight();
}
