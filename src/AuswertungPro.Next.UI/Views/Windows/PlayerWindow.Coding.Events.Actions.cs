using System;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        _player.SetPause(true);
        SuspendCodingOverlayInput();

        var entry = codingEvent.Entry;
        var explorerVm = CreateVsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath,
            TimeSpan.FromMilliseconds(_player.Time))
        {
            Owner = this,
            LiveSnapshotProvider = () =>
            {
                var snapPath = CodingLiveSnapshotPathPolicy.CreateTempPath();
                return TakeSnapshotSafe(snapPath) ? snapPath : null;
            }
        };

        bool? dialogResult;
        try
        {
            dialogResult = dlg.ShowDialog();
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (dialogResult == true && dlg.SelectedEntry is not null)
        {
            var result = dlg.SelectedEntry;
            CodingProtocolEntryCopier.CopyEditableValues(result, entry);

            codingEvent.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? codingEvent.MeterAtCapture;
            codingEvent.VideoTimestamp = entry.Zeit ?? codingEvent.VideoTimestamp;
            _codingSessionService?.UpdateEvent(codingEvent.EventId, entry, codingEvent.Overlay);

            RefreshCodingEventsList();
        }
    }

    private void CodingEventEdit_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is CodingEvent)
            CodingEvents_DoubleClick(sender, null!);
    }

    private void CodingEventSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        if (_player != null && CodingEventSeekPolicy.TryGetSeekMilliseconds(codingEvent, out var milliseconds))
            _player.Time = milliseconds;
    }

    private void CodingEventCloseStretch_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent startEvent) return;
        if (_codingSessionService == null || _codingVm == null) return;

        double currentMeter = _codingVm.CurrentMeter;
        if (!CodingStretchDamageClosePolicy.CanClose(startEvent.MeterAtCapture, currentMeter))
        {
            CodingEventActionDialogServiceFactory.Create().ShowStretchCloseRequiresLaterMeter();
            return;
        }

        var endEntry = CodingStreckenschadenEventFactory.CloseStart(startEvent.Entry, currentMeter);

        var endEvent = _codingSessionService.AddEvent(endEntry, null);
        endEvent.VideoTimestamp = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time) : TimeSpan.Zero;

        RefreshCodingEventsList();

        SetCodingAiState(
            CodingStretchDamageClosePolicy.BuildClosedStatusText(
                startEvent.Entry.Code,
                startEvent.MeterAtCapture,
                currentMeter),
            PlayerStatusColors.Success, "");
    }

    private void CodingEventDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        SuspendCodingOverlayInput();
        bool confirm;
        try
        {
            confirm = CodingEventActionDialogServiceFactory.Create().ConfirmDelete(codingEvent.Entry.Code);
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
        if (!confirm) return;

        _codingSessionService?.RemoveEvent(codingEvent.EventId);
        _codingVm?.Events.Remove(codingEvent);
        if (_codingVm != null && ReferenceEquals(_codingVm.SelectedDefect, codingEvent))
            _codingVm.SelectedDefect = null;
        HideInlineDefectDetail();
        RefreshCodingEventsList();
    }
}
