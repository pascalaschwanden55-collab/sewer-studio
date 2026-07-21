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
                    var commands = CodingTimelineCommandFactory.Create(
                        new CodingTimelineCommandBindings(
                            HasCodingSessionService: () => _codingSessionRuntimeOwner.Service is not null,
                            IsRunningOrPaused: () => _codingSessionHost.IsRunningOrPaused,
                            MoveToMeter: meter => _codingSessionRuntimeOwner.Service!.MoveToMeter(meter),
                            MarkNavigationPending: _codingNavigationPendingState.MarkPending,
                            SyncVideoToCodingMeter: SyncVideoToCodingMeter,
                            JumpToDefect: selectedEvent =>
                            {
                                _codingSessionHost.ExecuteJumpToDefect(selectedEvent);
                            },
                            SelectEvent: selectedEvent =>
                                _codingSidePanelControllers.EventsList.SelectEvent(selectedEvent)));

                    CodingTimelineControls.Configure(
                        PipeTimeline,
                        CodingTimelinePanel,
                        _codingSessionHost.EndMeter,
                        _codingSessionHost.Events,
                        commands.NavigateToMeter,
                        commands.MarkerClicked);
                }));
    }
}
