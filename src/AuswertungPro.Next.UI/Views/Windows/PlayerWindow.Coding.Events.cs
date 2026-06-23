using System;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async void CodingSelectCode_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;

        _player.SetPause(true);
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

            var entry = CodingExplorerEntryFactory.CreateSeed(
                _codingVm.CurrentOverlay,
                videoZeit);
            entry.MeterStart = meterValue;
            entry.MeterEnd = meterValue;

            var explorerVm = CreateVsaCodeExplorerViewModel(
                entry, meterValue, videoZeit);

            var dialogResult = VsaCodeExplorerDialogServiceFactory.Create().Show(
                explorerVm,
                _videoPath,
                videoZeit,
                this,
                CreateVsaCodeExplorerLiveSnapshotProvider());

            if (dialogResult.Accepted && dialogResult.SelectedEntry is not null)
            {
                var result = dialogResult.SelectedEntry;
                CodingProtocolEntryCopier.CopyEditableValues(result, entry);

                var createdEvent = _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);
                createdEvent.AiContext = CodingManualEventFactory.CreateUnconfirmedContext(entry.Code);

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
        if (_codingVm == null || string.IsNullOrWhiteSpace(_codingVm.SelectedCode)) return;

        _codingVm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);

        var draft = CodingManualEventFactory.CreateUnconfirmed(
            _codingVm.SelectedCode,
            _codingVm.SelectedCodeDescription,
            _codingLastOsdMeter ?? _codingVm.CurrentMeter,
            TimeSpan.FromMilliseconds(_player.Time),
            _codingVm.CurrentOverlay);

        var fotoPath = CodingCaptureSnapshot(draft.Entry);
        if (fotoPath != null)
            draft.Entry.FotoPaths.Add(fotoPath);

        var manualEvent = _codingSessionService!.AddEvent(draft.Entry, _codingVm.CurrentOverlay);
        manualEvent.AiContext = draft.AiContext;
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
        if (_codingVm == null) return;

        var sorted = CodingEventDisplayOrderPolicy.Order(_codingVm.Events);
        _codingEventsListControls.ApplyOrderedEvents(_codingVm.Events, sorted);

        Dispatcher.InvokeAsync(ColorizeCodingEventListItems, System.Windows.Threading.DispatcherPriority.Loaded);
        UpdateCodingStatistics();
    }

    private void UpdateCodingStatistics()
    {
        if (_codingVm == null) return;

        var summary = CodingStatisticsPolicy.Build(
            _codingVm.Events,
            CodingSessionViewModel.GetDefectStatus);

        _codingStatisticsControls.Apply(summary);
    }
}
