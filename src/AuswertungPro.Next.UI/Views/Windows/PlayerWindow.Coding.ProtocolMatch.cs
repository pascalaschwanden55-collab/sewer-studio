using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ImportEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    private void ImportSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    private void SeekToImportEvent(CodingEvent importEvent)
    {
        if (CodingEventSeekPolicy.TryGetSeekMilliseconds(importEvent, out var milliseconds))
            _playerTimelineHost.SeekMilliseconds(milliseconds);
        else if (_codingSessionRuntimeOwner.Service != null && importEvent.MeterAtCapture > 0)
        {
            _codingSessionRuntimeOwner.Service.MoveToMeter(importEvent.MeterAtCapture);
            _codingNavPending = true;
            SyncVideoToCodingMeter();
        }
    }

    private void RunCodingProtocolMatch_Click(object sender, RoutedEventArgs e)
    {
        RunCodingProtocolMatch();
    }

    private void RunCodingProtocolMatch()
    {
        if (!_codingSessionHost.HasViewModel) return;

        _lastCodingMatch = CodingProtocolMatchRunner.Run(
            _codingImportEvents,
            _codingSessionHost.Events,
            _codingProtocolMatchBuckets);
        UpdateCodingProtocolMatchSummary(_lastCodingMatch);
        RefreshCodingEventsList();
        Dispatcher.InvokeAsync(ApplyCodingProtocolMatchListHighlights, DispatcherPriority.Loaded);
    }

    private void UpdateCodingProtocolMatchSummary(CodingMatchRouting? routing)
    {
        CodingProtocolMatchSummaryControls.Apply(
            TxtCodingProtocolMatchSummary,
            BtnAcceptGreenCodingMatches,
            routing);
    }

}
