using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ImportEvents_DoubleClick(object sender, MouseButtonEventArgs e) => SeekToImportEvent();

    private void ImportSeek_Click(object sender, RoutedEventArgs e) => SeekToImportEvent();

    private void SeekToImportEvent()
        => SeekToImportEvent(LstImportEvents.SelectedItem);

    private void SeekToImportEvent(CodingEvent importEvent)
        => SeekToImportEvent((object?)importEvent);

    private void SeekToImportEvent(object? selectedItem)
    {
        var codingSessionService = _codingSessionRuntimeOwner.Service;
        CodingImportEventSeekCommandWorkflow.Execute(
            new CodingImportEventSeekCommandRequest(
                selectedItem,
                HasCodingSessionService: codingSessionService is not null),
            new CodingImportEventSeekCommandActions(
                SeekMilliseconds: _playerTimelineHost.SeekMilliseconds,
                MoveToMeter: meter => codingSessionService!.MoveToMeter(meter),
                MarkNavigationPending: () => _codingNavPending = true,
                SyncVideoToCodingMeter: SyncVideoToCodingMeter));
    }

    private void RunCodingProtocolMatch_Click(object sender, RoutedEventArgs e) => RunCodingProtocolMatch();

    private void RunCodingProtocolMatch()
    {
        CodingProtocolMatchCommandWorkflow.Execute(
            new CodingProtocolMatchCommandRequest(_codingSessionHost.HasViewModel),
            new CodingProtocolMatchCommandActions(
                RunMatch: () => CodingProtocolMatchRunner.Run(
                    _codingImportEvents,
                    _codingSessionHost.Events,
                    _codingProtocolMatchBuckets),
                StoreMatch: routing => _lastCodingMatch = routing,
                UpdateSummary: UpdateCodingProtocolMatchSummary,
                RefreshEvents: RefreshCodingEventsList,
                ScheduleHighlights: () =>
                    Dispatcher.InvokeAsync(ApplyCodingProtocolMatchListHighlights, DispatcherPriority.Loaded)));
    }

    private void UpdateCodingProtocolMatchSummary(CodingMatchRouting? routing)
    {
        CodingProtocolMatchSummaryControls.Apply(
            TxtCodingProtocolMatchSummary,
            BtnAcceptGreenCodingMatches,
            routing);
    }

}
