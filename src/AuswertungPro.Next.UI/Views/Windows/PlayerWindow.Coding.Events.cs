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
        if (_codingVm == null) return;

        PlayerCodingPlayback.PauseForCodingInteraction(pause => _player.SetPause(pause));
        SuspendCodingOverlayInput();

        try
        {
            var videoZeit = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));

            var osdMeter = await CodingReadOsdMeterAsync();
            var meterValue = CodingCurrentMeterResolver.ResolveManualEntry(
                osdMeter,
                _codingLastOsdMeter,
                _player.Time,
                _player.Length,
                _codingVm.EndMeter,
                _codingVm.CurrentMeter);

            var entry = CodingCodeExplorerWorkflowServiceFactory.Create(CreateVsaCodeExplorerViewModel)
                .CreateManualEntry(
                    _codingVm.CurrentOverlay,
                    meterValue,
                    videoZeit,
                    _videoPath,
                    this,
                    CreateVsaCodeExplorerLiveSnapshotProvider());

            if (entry is not null)
            {
                var createdEvent = CodingManualEventAppender.Apply(entry, _codingVm.CurrentOverlay, _codingSessionService!);

                RefreshCodingEventsList();
                LstCodingEvents.SelectedItem = createdEvent;

                _codingSchemaManager.Cancel();
                _codingVm.CurrentOverlay = null;
                RedrawCodingCanvas(includeManualOverlay: false);
                TxtCodingSelectedCode.Text = "";
                BtnCodingCreateEvent.IsEnabled = false;
                UpdateCodingOverlayInfo(null);
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }

    private void CodingCreateEvent_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;

        var videoTime = TimeSpan.FromMilliseconds(_player.Time);
        _codingVm.CurrentVideoTime = videoTime;
        var createdEvent = CodingSelectedCodeEventWorkflow.Create(
            _codingVm.SelectedCode,
            _codingVm.SelectedCodeDescription,
            _codingLastOsdMeter ?? _codingVm.CurrentMeter,
            videoTime,
            _codingVm.CurrentOverlay,
            _codingSessionService,
            CodingCaptureSnapshot);
        if (createdEvent == null)
            return;

        RefreshCodingEventsList();

        _codingSchemaManager.Cancel();
        _codingVm.CurrentOverlay = null;
        _codingVm.SelectedCode = "";
        _codingVm.SelectedCodeDescription = "";
        RedrawCodingCanvas(includeManualOverlay: false);
        TxtCodingSelectedCode.Text = "";
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
    }

    private void RefreshCodingEventsList()
    {
        if (!CodingEventsRefreshWorkflow.RefreshListAndStatistics(
                _codingVm?.Events,
                _codingEventsListControls,
                _codingStatisticsControls,
                CodingSessionViewModel.GetDefectStatus))
            return;

        Dispatcher.InvokeAsync(ColorizeCodingEventListItems, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void UpdateCodingStatistics()
    {
        CodingEventsRefreshWorkflow.RefreshStatistics(
            _codingVm?.Events,
            _codingStatisticsControls,
            CodingSessionViewModel.GetDefectStatus);
    }
}
