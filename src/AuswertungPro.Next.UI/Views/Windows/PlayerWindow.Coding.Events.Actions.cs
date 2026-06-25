using System;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        PlayerCodingPlayback.PauseForCodingInteraction(pause => _player.SetPause(pause));
        SuspendCodingOverlayInput();

        var entry = codingEvent.Entry;
        bool edited;
        try
        {
            edited = CodingCodeExplorerWorkflowServiceFactory.Create(CreateVsaCodeExplorerViewModel)
                .TryEdit(
                    entry,
                    entry.MeterStart,
                    entry.Zeit,
                    _videoPath,
                    _playerTimelineHost.CurrentTimeOrZero,
                    this,
                    CreateVsaCodeExplorerLiveSnapshotProvider());
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (edited)
        {
            CodingEventListActionWorkflow.CompleteEdit(
                codingEvent,
                _codingSessionRuntimeOwner.Service,
                RefreshCodingEventsList);
        }
    }

    private void CodingEventEdit_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is CodingEvent)
            CodingEvents_DoubleClick(sender, null!);
    }

    private void CodingEventSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        if (CodingEventSeekPolicy.TryGetSeekMilliseconds(codingEvent, out var milliseconds))
            _playerTimelineHost.SeekMilliseconds(milliseconds);
    }

    private void CodingEventCloseStretch_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent startEvent) return;
        if (!_codingSessionHost.HasViewModel) return;

        var closeAction = CodingEventListActionWorkflow.CloseStretch(
            startEvent,
            _codingSessionRuntimeOwner.Service,
            _codingSessionHost.CurrentMeter,
            _playerTimelineHost.CurrentTimeOrZero);

        if (!closeAction.Applied)
            return;

        if (closeAction.RequiresLaterMeterPrompt)
        {
            CodingEventActionDialogServiceFactory.Create().ShowStretchCloseRequiresLaterMeter();
            return;
        }

        if (closeAction.ShouldRefreshEvents)
            RefreshCodingEventsList();

        SetCodingAiState(closeAction.StatusText, PlayerStatusColors.Success, "");
    }

    private void CodingEventDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        SuspendCodingOverlayInput();
        bool confirm;
        try
        {
            confirm = CodingEventActionDialogServiceFactory.Create().ConfirmDelete(codingEvent.Entry.Code);
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
        if (!confirm) return;

        var deleteResult = CodingEventListActionWorkflow.Delete(
            codingEvent,
            _codingSessionRuntimeOwner.Service,
            _codingSessionHost.EventCollection,
            _codingSessionHost.SelectedDefect);
        if (!deleteResult.Deleted) return;

        if (deleteResult.ShouldClearSelectedDefect)
            _codingSessionHost.ClearSelectedDefect();
        HideInlineDefectDetail();
        RefreshCodingEventsList();
    }
}
