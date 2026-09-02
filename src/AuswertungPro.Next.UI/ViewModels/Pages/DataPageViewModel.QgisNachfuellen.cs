using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// "Leere Felder aus QGIS ergänzen" für die Haltungen.
///
/// Der Ablauf liegt in <see cref="QgisNachfuellWorkflow"/>, die Regeln in
/// <see cref="LeereFelderPlanBuilder"/> und <see cref="LeereFelderAnwender"/> —
/// hier steht nur die Verbindung zur Seite.
/// </summary>
public sealed partial class DataPageViewModel
{
    [RelayCommand]
    private void QgisFelderErgaenzen()
    {
        if (!_shell.IsProjectReady)
            return;

        var ergebnis = QgisNachfuellWorkflow.Fuehre(
            BauteilArt.Haltung,
            _qgisBestand,
            _dialogs,
            bestand => LeereFelderPlanBuilder.BaueFuerHaltungen(Records, bestand),
            plan => LeereFelderAnwender.WendeAnAufHaltungen(Records, plan));

        // Die Tabelle aktualisiert sich von selbst: HaltungRecord.SetFieldValue
        // meldet jede geaenderte Zelle ueber PropertyChanged.
        SaveStatus = ergebnis.Meldung;
        IsSaveStatusVisible = true;
    }
}
