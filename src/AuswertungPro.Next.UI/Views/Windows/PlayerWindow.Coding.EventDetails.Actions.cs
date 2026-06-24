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
        var acceptedDefect = CodingInlineDefectDecisionWorkflow.Accept(
            () => _codingSessionHost.SelectedDefect,
            () => { _codingSessionHost.ExecuteAcceptDefect(); },
            codingEvent => PersistSingleEventAsTrainingSample(codingEvent)
                .SafeFireAndForget("TrainingSaveAcceptInline"));

        if (acceptedDefect != null)
        {
            UpdateInlineDefectDetail(acceptedDefect);
            RefreshCodingEventsList();
            FadeOutAiOverlayAfterAction();
        }
    }

    private void CodingEditDefect_Click(object sender, RoutedEventArgs e)
    {
        if (!_codingSessionHost.HasViewModel)
            return;

        var ev = _codingSessionHost.SelectedDefect ?? LstCodingEvents.SelectedItem as CodingEvent;
        if (ev == null)
            return;

        _codingSessionHost.SelectDefect(ev);
        PlayerCodingPlayback.PauseForCodingInteraction(pause => _player.SetPause(pause));
        SuspendCodingOverlayInput();

        try
        {
            var entry = ev.Entry;
            var edited = CodingCodeExplorerWorkflowServiceFactory.Create(CreateVsaCodeExplorerViewModel)
                .TryEdit(
                    entry,
                    entry.MeterStart,
                    entry.Zeit,
                    _codingSessionHost.VideoPath,
                    _codingSessionHost.CurrentVideoTime,
                    this,
                    CreateVsaCodeExplorerLiveSnapshotProvider());

            if (edited)
            {
                var completed = CodingInlineDefectDecisionWorkflow.CompleteEdit(
                    ev,
                    _codingSessionService,
                    () => { _codingSessionHost.ExecuteEditDefect(); },
                    codingEvent => PersistSingleEventAsTrainingSample(codingEvent)
                        .SafeFireAndForget("TrainingSaveEditInline"));

                if (completed)
                {
                    RefreshCodingEventsList();
                    UpdateInlineDefectDetail(ev);
                }
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }

    private void CodingRejectDefect_Click(object sender, RoutedEventArgs e)
    {
        var rejectResult = CodingInlineDefectDecisionWorkflow.Reject(
            _codingSessionHost.SelectedDefect,
            LstCodingEvents.SelectedItem as CodingEvent,
            _codingSessionService,
            _codingSessionHost.EventCollection);

        if (!rejectResult.Rejected)
            return;

        if (rejectResult.ShouldClearSelectedDefect)
            _codingSessionHost.ClearSelectedDefect();
        HideInlineDefectDetail();
        RefreshCodingEventsList();
        FadeOutAiOverlayAfterAction();
    }
}
