using System;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingSelectCode_Click(object sender, RoutedEventArgs e)
    {
        HandleCodingSelectCodeAsync().SafeFireAndForget("CodingSelectCode");
    }

    private async Task HandleCodingSelectCodeAsync()
    {
        if (!_codingSessionHost.HasViewModel) return;

        PlayerCodingPlayback.PauseForCodingInteraction(pause => _player.SetPause(pause));
        SuspendCodingOverlayInput();

        try
        {
            var videoZeit = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));

            var osdMeter = await CodingReadOsdMeterAsync();
            var meterValue = CodingCurrentMeterResolver.ResolveManualEntry(
                osdMeter,
                _codingOsdMeterController.LastMeter,
                _player.Time,
                _player.Length,
                _codingSessionHost.EndMeter,
                _codingSessionHost.CurrentMeter);

            var entry = CodingCodeExplorerWorkflowServiceFactory.Create(CreateVsaCodeExplorerViewModel)
                .CreateManualEntry(
                    _codingSessionHost.CurrentOverlay,
                    meterValue,
                    videoZeit,
                    _videoPath,
                    this,
                    CreateVsaCodeExplorerLiveSnapshotProvider());

            if (entry is not null)
            {
                var createdEvent = CodingManualEventAppender.Apply(entry, _codingSessionHost.CurrentOverlay, _codingSessionRuntimeOwner.Service!);

                CodingEventCreationPostWorkflow.Apply(
                    createdEvent,
                    _codingEventCreationPostActions,
                    new CodingEventCreationPostOptions(
                        SelectCreatedEvent: true,
                        ClearSelectedCode: false));
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }

    private void CodingCreateEvent_Click(object sender, RoutedEventArgs e)
    {
        if (!_codingSessionHost.HasViewModel) return;

        var videoTime = TimeSpan.FromMilliseconds(_player.Time);
        _codingSessionHost.SetCurrentVideoTime(videoTime);
        var createdEvent = CodingSelectedCodeEventWorkflow.Create(
            _codingSessionHost.SelectedCode,
            _codingSessionHost.SelectedCodeDescription,
            _codingOsdMeterController.LastMeter ?? _codingSessionHost.CurrentMeter,
            videoTime,
            _codingSessionHost.CurrentOverlay,
            _codingSessionRuntimeOwner.Service,
            CodingCaptureSnapshot);
        if (createdEvent == null)
            return;

        CodingEventCreationPostWorkflow.Apply(
            createdEvent,
            _codingEventCreationPostActions,
            new CodingEventCreationPostOptions(
                SelectCreatedEvent: false,
                ClearSelectedCode: true));
    }

    private void RefreshCodingEventsList()
    {
        if (!CodingEventsRefreshWorkflow.RefreshListAndStatistics(
                _codingSessionHost.EventCollection,
                _codingEventsListControls,
                _codingStatisticsControls,
                CodingSessionViewModel.GetDefectStatus))
            return;

        Dispatcher.InvokeAsync(ColorizeCodingEventListItems, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void UpdateCodingStatistics()
    {
        CodingEventsRefreshWorkflow.RefreshStatistics(
            _codingSessionHost.HasViewModel ? _codingSessionHost.Events : null,
            _codingStatisticsControls,
            CodingSessionViewModel.GetDefectStatus);
    }
}
