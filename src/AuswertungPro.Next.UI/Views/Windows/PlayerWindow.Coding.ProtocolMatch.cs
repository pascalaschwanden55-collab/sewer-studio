using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
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
        if (_player != null && CodingEventSeekPolicy.TryGetSeekMilliseconds(importEvent, out var milliseconds))
            _player.Time = milliseconds;
        else if (_codingSessionService != null && importEvent.MeterAtCapture > 0)
        {
            _codingSessionService.MoveToMeter(importEvent.MeterAtCapture);
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
        if (_codingVm == null) return;

        _lastCodingMatch = CodingProtocolMatchService.Match(
            _codingImportEvents.Select(ev => ev.Entry).ToList(),
            _codingVm.Events.Select(ev => ev.Entry).ToList());

        CodingProtocolMatchBucketBuilder.Rebuild(_codingProtocolMatchBuckets, _lastCodingMatch);
        UpdateCodingProtocolMatchSummary(_lastCodingMatch);
        RefreshCodingEventsList();
        Dispatcher.InvokeAsync(ApplyCodingProtocolMatchListHighlights, DispatcherPriority.Loaded);
    }

    private void UpdateCodingProtocolMatchSummary(CodingMatchRouting? routing)
    {
        TxtCodingProtocolMatchSummary.Text = CodingProtocolMatchSummaryFormatter.Format(routing);
        BtnAcceptGreenCodingMatches.IsEnabled = CodingProtocolMatchSummaryFormatter.CanAcceptGreenMatches(routing);
    }

}
