using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingTimeline()
    {
        var codingVm = _codingVm ?? throw new InvalidOperationException("Coding timeline requires an active coding view model.");

        PipeTimeline.TotalLength = codingVm.EndMeter;
        PipeTimeline.MeterAccessor = CodingTimelineMarkerAccessors.Meter;
        PipeTimeline.CodeAccessor = CodingTimelineMarkerAccessors.Code;
        PipeTimeline.ConfidenceAccessor = CodingTimelineMarkerAccessors.Confidence;
        PipeTimeline.IsRejectedAccessor = CodingTimelineMarkerAccessors.IsRejected;
        PipeTimeline.Markers = codingVm.Events;
        PipeTimeline.NavigateToMeterCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<double>(meter =>
        {
            if (_codingSessionService != null && (codingVm.IsRunning || codingVm.IsPaused))
            {
                _codingSessionService.MoveToMeter(meter);
                _codingNavPending = true;
                SyncVideoToCodingMeter();
            }
        });
        PipeTimeline.MarkerClickedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<object>(item =>
        {
            if (item is CodingEvent ce)
            {
                codingVm.JumpToDefectCommand.Execute(ce);
                LstCodingEvents.SelectedItem = ce;
            }
        });
        CodingTimelinePanel.Visibility = Visibility.Visible;
    }
}
