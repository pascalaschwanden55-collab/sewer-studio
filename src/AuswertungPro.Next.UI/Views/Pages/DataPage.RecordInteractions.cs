using System;
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

    /// <summary>
    /// Abdocken und Andocken liegen im <see cref="DataPageDockingHost"/>; hier bleiben nur die
    /// beiden Schaltflaechen der Oberflaeche.
    /// </summary>
    private DataPageDockingHost? _docking;

    private void UndockGrid_Click(object sender, RoutedEventArgs e) => _docking?.Abdocken();

    private void DockBackFromPlaceholder_Click(object sender, RoutedEventArgs e) => _docking?.Andocken();


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
            Vm.InspectionProtocolFiles,
            Vm.ShellOpen,
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
    /// Chip-Filter aktualisieren. Die eigentliche Grid-Sicht erhaelt danach
    /// genau einen gemeinsamen Filter aus Suche, Chips und Dashboard-Startfilter.
    /// </summary>
    private void WendeChipFilterAn(DataPageFilter filter)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        _combinedFilter = _combinedFilter
            .WithSearchText(vm.SearchText)
            .WithChipFilter(filter);
        ApplyCombinedFilter(vm);
    }

    private void ApplyStartFilter()
    {
        if (_startFilterApplied || DataContext is not DataPageViewModel vm)
            return;

        _startFilterApplied = true;
        _combinedFilter = _combinedFilter
            .WithSearchText(vm.SearchText)
            .WithStartFilter(_combinedFilter.StartFilter ?? vm.StartFilter);
        FilterChips.SetStartFilter(_combinedFilter.StartFilter);

        if (_combinedFilter.StartFilter is not null)
        {
            HaltungsansichtToggle.IsChecked = false;
            HaltungsansichtView.Visibility = Visibility.Collapsed;
            Grid.Visibility = Visibility.Visible;
        }

        ApplyCombinedFilter(vm);
    }

    private void EntferneStartFilter()
    {
        if (_combinedFilter.StartFilter is null || DataContext is not DataPageViewModel vm)
            return;

        _combinedFilter = _combinedFilter
            .WithSearchText(vm.SearchText)
            .WithoutStartFilter();
        FilterChips.SetStartFilter(null);
        ApplyCombinedFilter(vm);
    }

    private void ApplyCombinedFilter(DataPageViewModel vm)
    {
        DataGridSearchFilterController.ApplyFilter(
            CollectionViewSource.GetDefaultView(vm.Records),
            vm.Records,
            getFilter: () =>
            {
                var filter = _combinedFilter.WithSearchText(vm.SearchText);
                return filter.IstAktiv ? filter.Passt : null;
            },
            updateSearchResultInfo: visibleCount => UpdateCombinedFilterUi(vm, visibleCount),
            deferRefresh: action => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, action));
    }

    private void UpdateCombinedFilterUi(DataPageViewModel vm, int visibleCount)
    {
        _combinedFilter = _combinedFilter.WithSearchText(vm.SearchText);

        vm.UpdateSearchResultInfo(visibleCount);
        Grid.AllowDrop = !_combinedFilter.IstAktiv;
        FilterChips.SetFilterActive(_combinedFilter.IstAktiv);
        FilterChips.SetTrefferInfo(visibleCount, vm.Records.Count);
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
