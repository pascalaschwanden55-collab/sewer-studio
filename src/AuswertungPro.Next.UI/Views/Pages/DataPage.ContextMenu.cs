using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

// DataPage Kontext-Menu-Handler: Alle MenuItem_Click-Routing-Methoden fuer
// Beobachtungen, Play, MoveUp/Down, Protocol, Relink, Costs, PrintAwu,
// OpenOriginalPdf, RestoreCosts, SuggestMeasures, MediaSearch, SanierungKi,
// VideoAiPipeline, Hydraulik (mit Print + Dossier), MoveToPosition, GoToRow.
// Aus dem Hauptdatei extrahiert (Slice 22b).
public partial class DataPage
{
    private void BeobachtungenMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Beobachtungen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        vm.Selected = record;

        var holdingName = record.GetFieldValue("Haltungsname");

        Action vsaUpdateAction = () =>
        {
            // Phase 5.1.B Etappe 3.H: via DI.
            AuswertungPro.Next.Application.Vsa.IVsaEvaluationService? vsa = null;
            try { vsa = App.Resolve<AuswertungPro.Next.Application.Vsa.IVsaEvaluationService>(); } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
            if (vsa is null) return;
            var res = vsa.EvaluateRecord(record);
            if (res.Ok)
            {
                vm.RefreshSelectedRecord();
                _dialogs.ShowMessage($"VSA Zustand aktualisiert für {holdingName}.",
                    "VSA", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _dialogs.ShowMessage($"VSA Fehler: {res.ErrorMessage}",
                    "VSA", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        Action syncHoldingFieldsAction = () =>
        {
            vm.SyncObservationsToHoldingFields(record, showStatus: true);
        };

        if (_beobachtungenWindow is not null && _beobachtungenWindow.IsLoaded)
        {
            _beobachtungenWindow.UpdateEntries(vm.SelectedProtocolEntries, holdingName, vsaUpdateAction, syncHoldingFieldsAction);
            _beobachtungenWindow.Activate();
            return;
        }

        _beobachtungenWindow = new BeobachtungenWindow(
            vm.SelectedProtocolEntries,
            holdingName,
            vm.OpenProtocolCommand,
            record,
            vsaUpdateAction,
            syncHoldingFieldsAction)
        {
            Owner = Window.GetWindow(this)
        };
        _beobachtungenWindow.Closed += (_, _) => _beobachtungenWindow = null;
        _beobachtungenWindow.Show();
    }

    private void PlayMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Video",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.PlayVideoCommand.Execute(record);
    }

    private void MoveRecordUpMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte zuerst eine Haltung auswaehlen.", "Position",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        vm.Selected = record;
        if (vm.MoveUpCommand.CanExecute(null))
            vm.MoveUpCommand.Execute(null);
    }

    private void MoveRecordDownMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte zuerst eine Haltung auswaehlen.", "Position",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        vm.Selected = record;
        if (vm.MoveDownCommand.CanExecute(null))
            vm.MoveDownCommand.Execute(null);
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.ContextMenu is null)
            return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement = PlacementMode.Bottom;
        btn.ContextMenu.DataContext = DataContext;
        btn.ContextMenu.IsOpen = true;
    }

    private void ProtocolMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Protokoll",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.OpenProtocolCommand.Execute(record);
    }

    private void RelinkMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Video",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.RelinkVideoCommand.Execute(record);
    }

    private void CostsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.OpenCostsCommand.Execute(record);
    }

    private void PrintAwuHaltungsprotokollMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Haltungsprotokoll AWU",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.PrintAwuHaltungsprotokollCommand.Execute(record);
    }

    private void OpenOriginalPdfMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "PDF",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.OpenOriginalPdfCommand.Execute(record);
    }

    private void RestoreCostsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Kosten/Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.RestoreCostsCommand.Execute(record);
    }

    private void SuggestMeasuresMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.SuggestMeasuresCommand.Execute(record);
    }

    private void SuggestAllMeasuresMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        vm.SuggestAllMeasuresCommand.Execute(null);
    }

    private void MediaSearchMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        vm.SearchAndLinkMediaCommand.Execute(null);
    }

    private void SanierungKiMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "KI Sanierung",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.OptimizeSanierungKiCommand.Execute(record);
    }

    private void VideoAiPipelineMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        vm.OpenVideoAiPipelineCommand.Execute(record);
    }

    private void HydraulikMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        vm.OpenHydraulikCommand.Execute(record);
    }

    private void HydraulikPrint_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        vm.PrintHydraulikCommand.Execute(record);
    }

    private void DossierPrint_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        vm.PrintDossierCommand.Execute(record);
    }

    private void MoveToPositionBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        MoveToPosition_Click(sender, e);
    }

    private void MoveToPosition_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        if (!int.TryParse(MoveToPositionBox.Text.Trim(), out var pos))
        {
            _dialogs.ShowMessage("Bitte eine gueltige Zahl eingeben.", "Position",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!vm.MoveToPosition(pos))
            _dialogs.ShowMessage("Verschieben nicht moeglich. Bitte Zeile auswaehlen.", "Position",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void GoToRowBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        GoToRow_Click(sender, e);
    }

    private void GoToRow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        if (!int.TryParse(GoToRowBox.Text.Trim(), out var row) || row < 1)
        {
            _dialogs.ShowMessage("Bitte eine gueltige Zeilennummer eingeben.", "Gehe zu Zeile",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var idx = row - 1;
        if (idx >= vm.Records.Count)
            idx = vm.Records.Count - 1;
        if (idx >= 0)
        {
            vm.Selected = vm.Records[idx];
            Grid.ScrollIntoView(vm.Selected);
        }
    }
}
