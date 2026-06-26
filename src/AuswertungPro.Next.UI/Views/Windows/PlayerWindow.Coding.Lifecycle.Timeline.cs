using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingTimeline()
    {
        CodingTimelineInitializationWorkflow.Execute(
            new CodingTimelineInitializationRequest(_codingSessionHost.HasViewModel),
            new CodingTimelineInitializationActions(
                ConfigureTimeline: () =>
                {
                    var navigateToMeterCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<double>(meter =>
                    {
                        CodingTimelineCommandWorkflow.NavigateToMeter(
                            new CodingTimelineNavigateRequest(
                                _codingSessionRuntimeOwner.Service is not null,
                                _codingSessionHost.IsRunningOrPaused,
                                meter),
                            new CodingTimelineNavigateActions(
                                MoveToMeter: value => _codingSessionRuntimeOwner.Service!.MoveToMeter(value),
                                MarkNavigationPending: _codingNavigationPendingState.MarkPending,
                                SyncVideoToCodingMeter: SyncVideoToCodingMeter));
                    });
                    var markerClickedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<object>(item =>
                    {
                        CodingTimelineCommandWorkflow.MarkerClicked(
                            item,
                            new CodingTimelineMarkerActions(
                                JumpToDefect: selectedEvent =>
                                {
                                    _codingSessionHost.ExecuteJumpToDefect(selectedEvent);
                                },
                                SelectEvent: selectedEvent => _codingSidePanelControllers.EventsList.SelectEvent(selectedEvent)));
                    });

                    CodingTimelineControls.Configure(
                        PipeTimeline,
                        CodingTimelinePanel,
                        _codingSessionHost.EndMeter,
                        _codingSessionHost.Events,
                        navigateToMeterCommand,
                        markerClickedCommand);
                }));
    }
}
