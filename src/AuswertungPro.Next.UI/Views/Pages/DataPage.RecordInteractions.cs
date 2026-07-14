using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class DataPage : UserControl
{
    private BeobachtungenWindow? _beobachtungenWindow;

    // --- Abdocken / Andocken ---
    private FloatingGridWindow? _floatingGridWindow;
    // Merkt sich, welche Ansicht (Tabelle ODER Haltungsansicht) gerade abgedockt ist,
    // damit sie beim Andocken an die richtige Stelle zurueckkommt.
    private UIElement? _undockedView;

    private void UndockGrid_Click(object sender, RoutedEventArgs e)
    {
        UndockGrid();
    }

    private void DockBackFromPlaceholder_Click(object sender, RoutedEventArgs e)
    {
        DockGridBack();
    }

    private void UndockGrid()
    {
        if (_floatingGridWindow is not null)
        {
            _floatingGridWindow.Activate();
            return;
        }

        try
        {
            // Guard-Flag setzen damit der Unloaded-Handler nicht interferiert
            _isUndocking = true;

            // Die aktuell gezeigte Ansicht abdocken: Haltungsansicht wenn der Umschalter
            // an ist, sonst die Tabelle. Umschalter sperren, solange abgedockt.
            var active = GridDockingController.ResolveActiveView(
                HaltungsansichtToggle.IsChecked == true,
                HaltungsansichtView,
                Grid);
            _undockedView = active;
            HaltungsansichtToggle.IsEnabled = false;

            // FloatingGridWindow erstellen (VOR dem Entfernen der Ansicht!)
            _floatingGridWindow = new FloatingGridWindow();
            _floatingGridWindow.DockBackRequested += DockGridBack;
            _floatingGridWindow.Closed += FloatingGridWindow_Closed;

            // DataContext auf FloatingWindow setzen (damit Bindings funktionieren)
            _floatingGridWindow.DataContext = DataContext;

            // Aktive Ansicht aus dem visuellen Baum entfernen und ins Floating-Fenster verschieben
            GridDockingController.ApplyUndockedState(
                GridHost,
                active,
                UndockedPlaceholder,
                UndockButton,
                HaltungsansichtToggle);
            _floatingGridWindow.SetGridContent(active);

            // Fensterposition aus Settings laden
            var settings = Settings;
            _floatingGridWindow.ApplySavedBounds(settings?.FloatingGridBounds);

            // Titel und Info aktualisieren
            UpdateFloatingWindowInfo();

            _floatingGridWindow.Show();

            // Settings merken
            if (settings is not null)
                settings.IsGridFloating = true;
        }
        catch (Exception ex)
        {
            // Bei Fehler: alles zuruecksetzen
            BestEffort.ReportWarning($"[DataPage] Abdocken fehlgeschlagen: {ex}");
            Dialogs.Warn($"Fehler beim Abdocken:\n{UserError.Describe(ex)}", "Abdocken");

            // Abgedockte Ansicht zuruecksetzen falls sie schon entfernt wurde
            GridDockingController.RestoreDockedState(
                GridHost,
                view: _undockedView,
                fallbackView: null,
                UndockedPlaceholder,
                UndockButton,
                HaltungsansichtToggle);
            _undockedView = null;

            if (_floatingGridWindow is not null)
            {
                _floatingGridWindow.DockBackRequested -= DockGridBack;
                _floatingGridWindow.Closed -= FloatingGridWindow_Closed;
                try { _floatingGridWindow.Close(); } catch { }
                _floatingGridWindow = null;
            }
        }
        finally
        {
            _isUndocking = false;
        }
    }

    private void DockGridBack()
    {
        if (_floatingGridWindow is null)
            return;

        // Fensterposition speichern
        var settings = Settings;
        if (settings is not null)
        {
            settings.FloatingGridBounds = _floatingGridWindow.GetBoundsString();
            settings.IsGridFloating = false;
        }

        // Ansicht aus dem Floating-Fenster entfernen
        var view = _floatingGridWindow.RemoveGridContent();
        _floatingGridWindow.DockBackRequested -= DockGridBack;
        _floatingGridWindow.Closed -= FloatingGridWindow_Closed;
        _floatingGridWindow.Close();
        _floatingGridWindow = null;

        RestoreUndockedView(view);
    }

    // Holt die abgedockte Ansicht (Tabelle ODER Haltungsansicht) zurueck in den GridHost.
    private void RestoreUndockedView(UIElement? view)
    {
        if (GridDockingController.RestoreDockedState(
            GridHost,
            view,
            _undockedView,
            UndockedPlaceholder,
            UndockButton,
            HaltungsansichtToggle))
        {
            _undockedView = null;
        }
    }

    private void FloatingGridWindow_Closed(object? sender, EventArgs e)
    {
        // Wenn das Floating-Fenster geschlossen wird (X-Button), Grid zurueck docken
        if (_floatingGridWindow is null)
            return;

        var settings = Settings;
        if (settings is not null)
        {
            settings.FloatingGridBounds = _floatingGridWindow.GetBoundsString();
            settings.IsGridFloating = false;
        }

        var view = _floatingGridWindow.RemoveGridContent();
        _floatingGridWindow.DockBackRequested -= DockGridBack;
        _floatingGridWindow = null;

        RestoreUndockedView(view);
    }

    private void UpdateFloatingWindowInfo()
    {
        if (_floatingGridWindow is null)
            return;

        var vm = DataContext as DataPageViewModel;
        var projectName = vm?.Project?.Name;
        var count = vm?.Records?.Count ?? 0;
        var selected = vm?.Selected?.GetFieldValue("Haltungsname");
        _floatingGridWindow.UpdateInfo(projectName, count, selected);
    }

    private void BeobachtungenMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var record = ResolveActionRecord(sender, vm);
        var request = _beobachtungenController.BuildOpenRequest(
            record,
            vm.SelectedProtocolEntries,
            vm.OpenProtocolCommand,
            value => vm.Selected = value,
            vm.RefreshSelectedRecord,
            (target, showStatus) => vm.SyncObservationsToHoldingFields(target, showStatus));
        if (request is null)
            return;

        ShowOrUpdateBeobachtungenWindow(request);
    }

    private void ShowOrUpdateBeobachtungenWindow(DataPageBeobachtungenWindowRequest request)
    {
        if (_beobachtungenWindow is not null && _beobachtungenWindow.IsLoaded)
        {
            _beobachtungenWindow.UpdateEntries(
                request.Entries,
                request.HoldingName,
                request.VsaUpdateAction,
                request.SyncHoldingFieldsAction);
            _beobachtungenWindow.Activate();
            return;
        }

        _beobachtungenWindow = new BeobachtungenWindow(
            request.Entries,
            Settings,
            request.HoldingName,
            request.OpenProtocolCommand,
            request.Record,
            request.VsaUpdateAction,
            request.SyncHoldingFieldsAction)
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

        ExecuteRecordMenuCommand(sender, vm, vm.PlayVideoCommand, "Video");
    }

    private void PlayGegenMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteRecordMenuCommand(sender, vm, vm.PlayGegenVideoCommand, "Gegeninspektion");
    }

    private void MoveRecordUpMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteMoveRecordMenuCommand(sender, vm, vm.MoveUpCommand);
    }

    private void MoveRecordDownMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteMoveRecordMenuCommand(sender, vm, vm.MoveDownCommand);
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        ButtonContextMenuOpener.OpenFromButton(sender, DataContext);
    }

    private void RelinkMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteRecordMenuCommand(sender, vm, vm.RelinkVideoCommand, "Video");
    }

    private void CostsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteRecordMenuCommand(sender, vm, vm.OpenCostsCommand, "Massnahmen");
    }

    private void PrintAwuHaltungsprotokollMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteRecordMenuCommand(sender, vm, vm.PrintAwuHaltungsprotokollCommand, "Haltungsprotokoll AWU");
    }

    private void OpenOriginalPdfMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteRecordMenuCommand(sender, vm, vm.OpenOriginalPdfCommand, "PDF");
    }

    private void OpenDichtheitPdfMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteRecordMenuCommand(sender, vm, vm.OpenDichtheitPdfCommand, "Dichtheitspruefung");
    }

    /// <summary>
    /// Chip-Filter auf die Grid-Sicht anwenden. Reiner View-Filter: die
    /// Records-Reihenfolge (= NR-Laufnummer) bleibt unangetastet; Zeilen-
    /// Verschieben per Drag&amp;Drop wird bei aktivem Filter gesperrt.
    /// </summary>
    private void WendeChipFilterAn(DataPageFilter filter)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(vm.Records);
        view.Filter = filter.IstAktiv
            ? o => filter.Passt(o as HaltungRecord)
            : null;

        Grid.AllowDrop = !filter.IstAktiv;
        FilterChips.SetTrefferInfo(view.Cast<object>().Count(), vm.Records.Count);
    }

    private void ApplyStartFilter()
    {
        if (_startFilterApplied || DataContext is not DataPageViewModel vm || vm.StartFilter is null)
            return;

        _startFilterApplied = true;
        HaltungsansichtToggle.IsChecked = false;
        HaltungsansichtView.Visibility = Visibility.Collapsed;
        Grid.Visibility = Visibility.Visible;

        var view = CollectionViewSource.GetDefaultView(vm.Records);
        view.Filter = obj => vm.StartFilter.Matches(obj as HaltungRecord);
        view.Refresh();

        Grid.AllowDrop = false;
        FilterChips.SetTrefferInfo(view.Cast<object>().Count(), vm.Records.Count);
    }

    private void OpenContainingFolderMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteRecordMenuCommand(sender, vm, vm.OpenContainingFolderCommand, "Ordner");
    }

    private void RestoreCostsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteRecordMenuCommand(sender, vm, vm.RestoreCostsCommand, "Kosten/Massnahmen");
    }

    private void SuggestMeasuresMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        ExecuteRecordMenuCommand(sender, vm, vm.SuggestMeasuresCommand, "Massnahmen");
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

    private void HydraulikMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = DataPageContextMenuRecordResolver.Resolve(sender, vm.Selected);
        vm.OpenHydraulikCommand.Execute(record);
    }

    private void HydraulikPrint_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = DataPageContextMenuRecordResolver.Resolve(sender, vm.Selected);
        vm.PrintHydraulikCommand.Execute(record);
    }

    private void DossierPrint_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = DataPageContextMenuRecordResolver.Resolve(sender, vm.Selected);
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

        DataPageRowNavigationController.TryMoveToPosition(
            MoveToPositionBox.Text,
            vm.MoveToPosition,
            Dialogs.Info);
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

        if (DataPageRowNavigationController.TryResolveRowIndex(
            GoToRowBox.Text,
            vm.Records.Count,
            Dialogs.Info,
            out var rowIndex))
        {
            vm.Selected = vm.Records[rowIndex];
            Grid.ScrollIntoView(vm.Selected);
        }
    }

    private static HaltungRecord? ResolveActionRecord(object sender, DataPageViewModel vm)
        => DataPageContextMenuRecordResolver.Resolve(sender, vm.Selected);

    private void ExecuteRecordMenuCommand(
        object sender,
        DataPageViewModel vm,
        ICommand command,
        string missingSelectionTitle)
        => DataPageRecordCommandRouter.TryExecute(
            ResolveActionRecord(sender, vm),
            command,
            Dialogs.Info,
            missingSelectionTitle);

    private void ExecuteMoveRecordMenuCommand(
        object sender,
        DataPageViewModel vm,
        ICommand command)
        => DataPageRecordCommandRouter.TrySelectAndExecute(
            DataPageContextMenuRecordResolver.Resolve(sender, vm.Selected),
            record => vm.Selected = record,
            command,
            Dialogs.Info,
            missingSelectionTitle: "Position");

}
