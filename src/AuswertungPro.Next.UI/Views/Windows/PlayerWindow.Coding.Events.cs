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
        await CodingSelectCodeCommandWorkflow.ExecuteAsync(
            new CodingSelectCodeCommandRequest(_codingSessionHost.HasViewModel),
            new CodingSelectCodeCommandActions(
                PauseForCodingInteraction: () => PlayerCodingPlayback.PauseForCodingInteraction(
                    _playerPlaybackControlHost.SetPause),
                RunWithSuspendedOverlayInputAsync: RunWithSuspendedCodingOverlayInputAsync,
                GetCurrentVideoTime: () => _playerTimelineHost.CurrentTimeOrZero,
                ReadOsdMeterAsync: CodingReadOsdMeterAsync,
                ResolveManualEntryMeter: osdMeter => CodingCurrentMeterResolver.ResolveManualEntry(
                    osdMeter,
                    _codingOsdMeterController.LastMeter,
                    _playerTimelineHost.TimeMilliseconds ?? 0,
                    _playerTimelineHost.LengthMilliseconds ?? 0,
                    _codingSessionHost.EndMeter,
                    _codingSessionHost.CurrentMeter),
                CreateManualEntry: (videoTime, meterValue) => CodingCodeExplorerWorkflowServiceFactory
                    .Create(CreateVsaCodeExplorerViewModel)
                    .CreateManualEntry(
                        _codingSessionHost.CurrentOverlay,
                        meterValue,
                        videoTime,
                        _videoPath,
                        this,
                        CreateVsaCodeExplorerLiveSnapshotProvider()),
                AppendManualEvent: entry => CodingManualEventAppender.Apply(
                    entry,
                    _codingSessionHost.CurrentOverlay,
                    _codingSessionRuntimeOwner.Service!),
                ApplyPostCreation: createdEvent => CodingEventCreationPostWorkflow.Apply(
                    createdEvent,
                    _codingEventCreationPostActions,
                    new CodingEventCreationPostOptions(
                        SelectCreatedEvent: true,
                        ClearSelectedCode: false))));
    }

    private void CodingCreateEvent_Click(object sender, RoutedEventArgs e)
    {
        CodingCreateSelectedCodeEventCommandWorkflow.Execute(
            new CodingCreateSelectedCodeEventCommandRequest(_codingSessionHost.HasViewModel),
            new CodingCreateSelectedCodeEventCommandActions(
                GetCurrentVideoTime: () => _playerTimelineHost.CurrentTimeOrZero,
                SetCurrentVideoTime: _codingSessionHost.SetCurrentVideoTime,
                CreateEvent: videoTime => CodingSelectedCodeEventWorkflow.Create(
                    _codingSessionHost.SelectedCode,
                    _codingSessionHost.SelectedCodeDescription,
                    _codingOsdMeterController.LastMeter ?? _codingSessionHost.CurrentMeter,
                    videoTime,
                    _codingSessionHost.CurrentOverlay,
                    _codingSessionRuntimeOwner.Service,
                    CodingCaptureSnapshot),
                ApplyPostCreation: createdEvent => CodingEventCreationPostWorkflow.Apply(
                    createdEvent,
                    _codingEventCreationPostActions,
                    new CodingEventCreationPostOptions(
                        SelectCreatedEvent: false,
                        ClearSelectedCode: true))));
    }

    private void RefreshCodingEventsList()
    {
        CodingEventsListRefreshCommandWorkflow.Execute(
            new CodingEventsListRefreshCommandActions(
                RefreshListAndStatistics: () => CodingEventsRefreshWorkflow.RefreshListAndStatistics(
                    _codingSessionHost.EventCollection,
                    _codingEventsListControls,
                    _codingStatisticsControls,
                    CodingSessionViewModel.GetDefectStatus),
                ScheduleColorize: () => Dispatcher.InvokeAsync(
                    ColorizeCodingEventListItems,
                    System.Windows.Threading.DispatcherPriority.Loaded)));
    }

    private void UpdateCodingStatistics()
    {
        CodingStatisticsUpdateCommandWorkflow.Execute(
            new CodingStatisticsUpdateCommandRequest(_codingSessionHost.HasViewModel),
            new CodingStatisticsUpdateCommandActions(
                RefreshStatistics: () => CodingEventsRefreshWorkflow.RefreshStatistics(
                    _codingSessionHost.Events,
                    _codingStatisticsControls,
                    CodingSessionViewModel.GetDefectStatus)));
    }
}
