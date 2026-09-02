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
    // Der Knopf haengt an CanMutateShaftData — derselben Schranke wie die uebrigen
    // aendernden Aktionen der Seite. Sie sperrt auch waehrend eines laufenden
    // Protokollimports, und die Seite meldet ihre Aenderung bereits. Eine eigene
    // Eigenschaft ohne Meldung waere beim Laden einmal ausgewertet und danach
    // fuer immer grau geblieben.
    [RelayCommand]
    private void QgisFelderErgaenzen()
    {
        if (QgisBestand is null || !CanMutateShaftData)
            return;

        QgisNachfuellWorkflow.Fuehre(
            BauteilArt.Schacht,
            QgisBestand,
            _dialogs,
            bestand => LeereFelderPlanBuilder.BaueFuerSchaechte(Records, bestand),
            plan => LeereFelderAnwender.WendeAnAufSchaechte(Records, plan));
    }
}
