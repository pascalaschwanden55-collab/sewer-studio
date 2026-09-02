using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// "Leere Felder aus QGIS ergänzen" für die Schächte — derselbe Ablauf wie bei
/// den Haltungen, nur mit der anderen Bauteilart.
/// </summary>
public sealed partial class SchaechtePageViewModel
{
    /// <summary>Der Knopf bleibt aus, solange der Dienst fehlt oder kein Projekt offen ist.</summary>
    public bool CanQgisFelderErgaenzen => QgisBestand is not null && _shell.IsProjectReady;

    [RelayCommand]
    private void QgisFelderErgaenzen()
    {
        if (QgisBestand is null || !_shell.IsProjectReady)
            return;

        QgisNachfuellWorkflow.Fuehre(
            BauteilArt.Schacht,
            QgisBestand,
            _dialogs,
            bestand => LeereFelderPlanBuilder.BaueFuerSchaechte(Records, bestand),
            plan => LeereFelderAnwender.WendeAnAufSchaechte(Records, plan));
    }
}
