using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingTimeline()
    {
        if (!_codingSessionHost.HasViewModel)
            throw new InvalidOperationException("Coding timeline requires an active coding view model.");

        var navigateToMeterCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<double>(meter =>
        {
            if (_codingSessionRuntimeOwner.Service != null && _codingSessionHost.IsRunningOrPaused)
            {
                _codingSessionRuntimeOwner.Service.MoveToMeter(meter);
                _codingNavPending = true;
                SyncVideoToCodingMeter();
            }
        });
        var markerClickedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<object>(item =>
        {
            if (item is CodingEvent ce)
            {
                _codingSessionHost.ExecuteJumpToDefect(ce);
                LstCodingEvents.SelectedItem = ce;
            }
        });

        CodingTimelineControls.Configure(
            PipeTimeline,
            CodingTimelinePanel,
            _codingSessionHost.EndMeter,
            _codingSessionHost.Events,
            navigateToMeterCommand,
            markerClickedCommand);
    }
}
