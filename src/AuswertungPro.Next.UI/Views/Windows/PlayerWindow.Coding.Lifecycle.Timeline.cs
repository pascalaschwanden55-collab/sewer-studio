using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingTimeline()
    {
        var codingVm = _codingVm ?? throw new InvalidOperationException("Coding timeline requires an active coding view model.");

        var navigateToMeterCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<double>(meter =>
        {
            if (_codingSessionService != null && (codingVm.IsRunning || codingVm.IsPaused))
            {
                _codingSessionService.MoveToMeter(meter);
                _codingNavPending = true;
                SyncVideoToCodingMeter();
            }
        });
        var markerClickedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<object>(item =>
        {
            if (item is CodingEvent ce)
            {
                codingVm.JumpToDefectCommand.Execute(ce);
                LstCodingEvents.SelectedItem = ce;
            }
        });

        CodingTimelineControls.Configure(
            PipeTimeline,
            CodingTimelinePanel,
            codingVm.EndMeter,
            codingVm.Events,
            navigateToMeterCommand,
            markerClickedCommand);
    }
}
