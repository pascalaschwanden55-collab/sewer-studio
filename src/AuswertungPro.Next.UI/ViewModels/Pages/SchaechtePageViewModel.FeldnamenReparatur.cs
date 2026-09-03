using System.Linq;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// „Feldnamen aufräumen": Führt doppelte Schreibweisen desselben Schachtfeldes
/// zusammen. Erst ein Bericht, dann die Rückfrage — wie beim Nachfüllen aus QGIS.
/// </summary>
public sealed partial class SchaechtePageViewModel
{
    [RelayCommand]
    private void FeldnamenAufraeumen()
    {
        if (!CanMutateShaftData)
            return;

        const string titel = "Feldnamen aufräumen";

        var plan = SchachtFeldnamenReparaturLauf.Plane(Records, Columns.ToList());
        var bericht = SchachtFeldnamenReparaturLauf.Bericht(plan);

        if (plan.OhneAenderung)
        {
            _dialogs.Info(bericht, titel);
            LastResult = "Feldnamen sind sauber.";
            return;
        }

        if (_dialogs.ConfirmCancel($"{bericht}\n\nJetzt zusammenführen?", titel) != DialogConfirm.Yes)
        {
            LastResult = "Abgebrochen.";
            return;
        }

        var entfernt = SchachtFeldnamenReparaturLauf.Wende(plan);

        // Die Tabelle bindet auf die Feldnamen; nach dem Zusammenführen muss sie
        // neu aufgebaut werden, sonst zeigen Spalten auf entfernte Felder.
        LoadColumnsFromTemplate();
        LastResult = $"{entfernt} doppelte Schreibweisen zusammengeführt. Nicht vergessen zu speichern.";
    }
}
