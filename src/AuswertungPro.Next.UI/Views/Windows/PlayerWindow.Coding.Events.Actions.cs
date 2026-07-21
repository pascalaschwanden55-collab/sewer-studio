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
        CodingEventEditCommandWorkflow.Execute(
            new CodingEventEditCommandRequest(LstCodingEvents.SelectedItem as CodingEvent),
            new CodingEventEditCommandActions(
                PausePlayback: () => PlayerCodingPlayback.PauseForCodingInteraction(_playerPlaybackControlHost.SetPause),
                TryEdit: TryEditCodingEvent,
                CompleteEdit: codingEvent => CodingEventListActionWorkflow.CompleteEdit(
                    codingEvent,
                    _codingSessionRuntimeOwner.Service,
                    RefreshCodingEventsList)));
    }

    private void CodingEventEdit_Click(object sender, RoutedEventArgs e)
    {
        CodingEventEditButtonCommandWorkflow.Execute(
            new CodingEventEditButtonCommandRequest(LstCodingEvents.SelectedItem),
            new CodingEventEditButtonCommandActions(
                EditSelectedEvent: _ => CodingEvents_DoubleClick(sender, null!)));
    }

    private void CodingEventSeek_Click(object sender, RoutedEventArgs e)
    {
        CodingEventSeekCommandWorkflow.Execute(
            new CodingEventSeekCommandRequest(LstCodingEvents.SelectedItem as CodingEvent),
            new CodingEventSeekCommandActions(_playerTimelineHost.SeekMilliseconds));
    }

    private void CodingEventCloseStretch_Click(object sender, RoutedEventArgs e)
    {
        CodingEventCloseStretchCommandWorkflow.Execute(
            new CodingEventCloseStretchCommandRequest(
                LstCodingEvents.SelectedItem as CodingEvent,
                _codingSessionHost.HasViewModel),
            new CodingEventCloseStretchCommandActions(
                CloseStretch: startEvent => CodingEventListActionWorkflow.CloseStretch(
                    startEvent,
                    _codingSessionRuntimeOwner.Service,
                    _codingSessionHost.CurrentMeter,
                    _playerTimelineHost.CurrentTimeOrZero),
                ShowRequiresLaterMeterPrompt: CodingEventActionDialogWorkflow.ShowStretchCloseRequiresLaterMeter,
                RefreshEvents: RefreshCodingEventsList,
                ShowSuccessStatus: status => _liveDetectionStatusController.SetCodingAiState(status, PlayerStatusColors.Success, "")));
    }

    private void CodingEventDelete_Click(object sender, RoutedEventArgs e)
    {
        CodingEventDeleteCommandWorkflow.Execute(
            new CodingEventDeleteCommandRequest(LstCodingEvents.SelectedItem as CodingEvent),
            new CodingEventDeleteCommandActions(
                ConfirmDelete: code => CodingEventActionDialogWorkflow.ConfirmDelete(
                    code,
                    runWithSuspendedOverlay: callback => _codingOverlayInputVisibilityController.Run(callback)),
                Delete: codingEvent => CodingEventListActionWorkflow.Delete(
                    codingEvent,
                    _codingSessionRuntimeOwner.Service,
                    _codingSessionHost.EventCollection,
                    _codingSessionHost.SelectedDefect),
                ClearSelectedDefect: _codingSessionHost.ClearSelectedDefect,
                HideInlineDefectDetail: HideInlineDefectDetail,
                RefreshEvents: RefreshCodingEventsList));
    }

    private bool TryEditCodingEvent(CodingEvent codingEvent)
        => CodingCodeExplorerEditWorkflow.Execute(
            new CodingCodeExplorerEditWorkflowRequest(
                codingEvent,
                _playbackContext.VideoPath,
                _playerTimelineHost.CurrentTimeOrZero,
                this),
            CreateCodingCodeExplorerEditActions());
}
