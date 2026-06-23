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
        _codingVm?.AcceptDefectCommand.Execute(null);
        if (_codingVm?.SelectedDefect != null)
        {
            // Mensch akzeptiert = bestaetigtes Gold -> als Trainingssample sichern (eval-geschuetzt).
            PersistSingleEventAsTrainingSample(_codingVm.SelectedDefect)
                .SafeFireAndForget("TrainingSaveAcceptInline");
            UpdateInlineDefectDetail(_codingVm.SelectedDefect);
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
        _player.SetPause(true);
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
                _codingSessionService?.UpdateEvent(ev.EventId, entry, ev.Overlay);
                ev.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? ev.MeterAtCapture;
                ev.VideoTimestamp = entry.Zeit ?? ev.VideoTimestamp;

                if (ev.AiContext != null)
                    _codingVm.EditDefectCommand.Execute(null);

                // Bearbeitet+uebernommen = korrigiertes Gold -> als Trainingssample sichern.
                PersistSingleEventAsTrainingSample(ev).SafeFireAndForget("TrainingSaveEditInline");
                RefreshCodingEventsList();
                UpdateInlineDefectDetail(ev);
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }

    private void CodingRejectDefect_Click(object sender, RoutedEventArgs e)
    {
        var ev = _codingVm?.SelectedDefect ?? LstCodingEvents.SelectedItem as CodingEvent;
        if (ev == null || _codingVm == null)
            return;

        // Ablehnen = Eintrag komplett entfernen, nicht nur Status setzen.
        _codingSessionService?.RemoveEvent(ev.EventId);
        _codingVm.Events.Remove(ev);
        _codingVm.SelectedDefect = null;
        HideInlineDefectDetail();
        RefreshCodingEventsList();
        FadeOutAiOverlayAfterAction();
    }
}
