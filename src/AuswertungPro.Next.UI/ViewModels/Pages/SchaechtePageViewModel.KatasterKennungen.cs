using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// "Katasterkennungen ergänzen" für die Schächte — derselbe Ablauf wie bei den
/// Haltungen, nur mit der anderen Bauteilart.
/// </summary>
public sealed partial class SchaechtePageViewModel
{
    // Haengt wie "Leere Felder aus QGIS" an CanMutateShaftData — derselben Schranke
    // wie die uebrigen aendernden Aktionen der Seite.
    [RelayCommand]
    private void KatasterKennungenErgaenzen()
    {
        if (KatasterKennungen is null || !CanMutateShaftData)
            return;

        KatasterKennungWorkflow.Fuehre(
            BauteilArt.Schacht,
            KatasterKennungen,
            _dialogs,
            bestand => KatasterKennungPlanBuilder.BaueFuerSchaechte(Records, bestand),
            plan => KatasterKennungAnwender.WendeAnAufSchaechte(Records, plan));
    }
}
