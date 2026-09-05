using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// "Katasterkennungen ergänzen" für die Haltungen.
///
/// Der Ablauf liegt in <see cref="KatasterKennungWorkflow"/>, die Regeln in
/// <see cref="KatasterKennungPlanBuilder"/> und <see cref="KatasterKennungAnwender"/> —
/// hier steht nur die Verbindung zur Seite.
/// </summary>
public sealed partial class DataPageViewModel
{
    [RelayCommand]
    private void KatasterKennungenErgaenzen()
    {
        if (!_shell.IsProjectReady)
            return;

        var ergebnis = KatasterKennungWorkflow.Fuehre(
            BauteilArt.Haltung,
            _katasterKennungen,
            _dialogs,
            bestand => KatasterKennungPlanBuilder.BaueFuerHaltungen(Records, bestand),
            plan => KatasterKennungAnwender.WendeAnAufHaltungen(Records, plan));

        SaveStatus = ergebnis.Meldung;
        IsSaveStatusVisible = true;
        if (ergebnis.Ausgefuehrt)
            MeldeFelderExternErgaenzt();
    }
}
