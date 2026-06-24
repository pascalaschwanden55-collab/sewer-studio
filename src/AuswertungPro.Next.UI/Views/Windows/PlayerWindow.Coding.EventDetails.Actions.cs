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
            () => _codingVm?.SelectedDefect,
            () => _codingVm?.AcceptDefectCommand.Execute(null),
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
        if (_codingVm == null)
            return;

        var ev = _codingVm.SelectedDefect ?? LstCodingEvents.SelectedItem as CodingEvent;
        if (ev == null)
            return;

        _codingVm.SelectedDefect = ev;
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
                    _codingVm.VideoPath,
                    _codingVm.CurrentVideoTime,
                    this,
                    CreateVsaCodeExplorerLiveSnapshotProvider());

            if (edited)
            {
                var completed = CodingInlineDefectDecisionWorkflow.CompleteEdit(
                    ev,
                    _codingSessionService,
                    () => _codingVm.EditDefectCommand.Execute(null),
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
            _codingVm?.SelectedDefect,
            LstCodingEvents.SelectedItem as CodingEvent,
            _codingSessionService,
            _codingVm?.Events);

        if (!rejectResult.Rejected || _codingVm == null)
            return;

        if (rejectResult.ShouldClearSelectedDefect)
            _codingVm.SelectedDefect = null;
        HideInlineDefectDetail();
        RefreshCodingEventsList();
        FadeOutAiOverlayAfterAction();
    }
}
