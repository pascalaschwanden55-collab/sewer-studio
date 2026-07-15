using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingAcceptDefect_Click(object sender, RoutedEventArgs e)
    {
        CodingInlineDefectAcceptCommandWorkflow.Execute(
            new CodingInlineDefectAcceptCommandActions(
                AcceptDefect: () => CodingInlineDefectDecisionWorkflow.Accept(
                    () => _codingSessionHost.SelectedDefect,
                    () => { _codingSessionHost.ExecuteAcceptDefect(); },
                    codingEvent => _codingTrainingPersistenceContext.PersistSingleEventAsync(codingEvent)
                        .SafeFireAndForget("TrainingSaveAcceptInline")),
                UpdateInlineDefectDetail: UpdateInlineDefectDetail,
                RefreshEvents: RefreshCodingEventsList,
                FadeOutAiOverlayAfterAction: FadeOutAiOverlayAfterAction));
    }

    private void CodingEditDefect_Click(object sender, RoutedEventArgs e)
    {
        CodingInlineDefectEditCommandWorkflow.Execute(
            new CodingInlineDefectEditCommandRequest(
                _codingSessionHost.HasViewModel,
                _codingSessionHost.SelectedDefect,
                LstCodingEvents.SelectedItem as CodingEvent),
            new CodingInlineDefectEditCommandActions(
                SelectDefect: _codingSessionHost.SelectDefect,
                PausePlayback: () => PlayerCodingPlayback.PauseForCodingInteraction(_playerPlaybackControlHost.SetPause),
                TryEdit: TryEditInlineDefect,
                CompleteEdit: CompleteInlineDefectEdit,
                RefreshEvents: RefreshCodingEventsList,
                UpdateInlineDefectDetail: UpdateInlineDefectDetail));
    }

    private bool TryEditInlineDefect(CodingEvent codingEvent)
        => CodingCodeExplorerEditWorkflow.Execute(
            new CodingCodeExplorerEditWorkflowRequest(
                codingEvent,
                _codingSessionHost.VideoPath,
                _codingSessionHost.CurrentVideoTime,
                this),
            CreateCodingCodeExplorerEditActions());

    private bool CompleteInlineDefectEdit(CodingEvent codingEvent)
    {
        return CodingInlineDefectDecisionWorkflow.CompleteEdit(
            codingEvent,
            _codingSessionRuntimeOwner.Service,
            () => { _codingSessionHost.ExecuteEditDefect(); },
            editedEvent => _codingTrainingPersistenceContext.PersistSingleEventAsync(editedEvent)
                .SafeFireAndForget("TrainingSaveEditInline"));
    }

    private void CodingRejectDefect_Click(object sender, RoutedEventArgs e)
    {
        CodingInlineDefectRejectCommandWorkflow.Execute(
            new CodingInlineDefectRejectCommandActions(
                RejectDefect: () => CodingInlineDefectDecisionWorkflow.Reject(
                    _codingSessionHost.SelectedDefect,
                    LstCodingEvents.SelectedItem as CodingEvent,
                    _codingSessionRuntimeOwner.Service,
                    _codingSessionHost.EventCollection),
                ClearSelectedDefect: _codingSessionHost.ClearSelectedDefect,
                HideInlineDefectDetail: HideInlineDefectDetail,
                RefreshEvents: RefreshCodingEventsList,
                FadeOutAiOverlayAfterAction: FadeOutAiOverlayAfterAction));
    }
}
