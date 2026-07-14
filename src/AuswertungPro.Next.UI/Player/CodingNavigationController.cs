using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingNavigationControllerActions(
    Action<double> ApplyMeterTimeline,
    Action<OverlayGeometry?> UpdateOverlayInfo,
    Action<CodingCurrentCodeBadgeState> ApplyCurrentCodeState,
    Action UpdateStatistics,
    Action PausePlayback,
    Func<Task<double?>> ReadOsdMeterAsync,
    Action<string> TraceError);

public sealed class CodingNavigationController
{
    private readonly ICodingSessionHost _sessionHost;
    private readonly CodingNavigationPendingState _pendingState;
    private readonly CodingOsdMeterController _osdMeterController;
    private readonly PlayerTimelineHost _timelineHost;
    private readonly CodingNavigationControllerActions _actions;

    public CodingNavigationController(
        ICodingSessionHost sessionHost,
        CodingNavigationPendingState pendingState,
        CodingOsdMeterController osdMeterController,
        PlayerTimelineHost timelineHost,
        CodingNavigationControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(sessionHost);
        ArgumentNullException.ThrowIfNull(pendingState);
        ArgumentNullException.ThrowIfNull(osdMeterController);
        ArgumentNullException.ThrowIfNull(timelineHost);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ApplyMeterTimeline);
        ArgumentNullException.ThrowIfNull(actions.UpdateOverlayInfo);
        ArgumentNullException.ThrowIfNull(actions.ApplyCurrentCodeState);
        ArgumentNullException.ThrowIfNull(actions.UpdateStatistics);
        ArgumentNullException.ThrowIfNull(actions.PausePlayback);
        ArgumentNullException.ThrowIfNull(actions.ReadOsdMeterAsync);
        ArgumentNullException.ThrowIfNull(actions.TraceError);

        _sessionHost = sessionHost;
        _pendingState = pendingState;
        _osdMeterController = osdMeterController;
        _timelineHost = timelineHost;
        _actions = actions;
    }

    public CodingUiUpdateCommandResult UpdateUi(string? propertyName)
    {
        var result = CodingUiUpdateCommandWorkflow.Execute(
            new CodingUiUpdateCommandRequest(
                _sessionHost.HasViewModel,
                propertyName,
                _pendingState.IsPending),
            new CodingUiUpdateCommandActions(
                ApplyUiUpdate: (changedPropertyName, navigationPending) => CodingUiUpdateWorkflow.Apply(
                    changedPropertyName,
                    navigationPending,
                    new CodingUiUpdateActions(
                        ApplyMeterTimeline: () => _actions.ApplyMeterTimeline(_sessionHost.CurrentMeter),
                        SyncVideoToCodingMeter: SyncVideoToCodingMeter,
                        UpdateOverlayInfo: () => _actions.UpdateOverlayInfo(_sessionHost.CurrentOverlay),
                        UpdateCurrentCode: () => UpdateCurrentCode(),
                        UpdateStatistics: _actions.UpdateStatistics))));
        _pendingState.Set(result.NavigationPending);
        return result;
    }

    public CodingCurrentCodeUpdateResult UpdateCurrentCode()
        => CodingCurrentCodeUpdateWorkflow.Execute(
            new CodingCurrentCodeUpdateRequest(_sessionHost.HasViewModel),
            new CodingCurrentCodeUpdateActions(
                GetEvents: () => _sessionHost.Events,
                ResolveCurrentMeter: ResolveCurrentDisplayMeter,
                ApplyState: _actions.ApplyCurrentCodeState));

    public void SyncVideoToCodingMeter()
        => CodingVideoSyncCommandWorkflow.Execute(
            new CodingVideoSyncCommandRequest(_sessionHost.HasViewModel),
            new CodingVideoSyncCommandActions(
                SyncVideoToCodingMeter: () => CodingVideoNavigationController.SyncVideoToCodingMeter(
                    _sessionHost.CurrentMeter,
                    _sessionHost.EndMeter,
                    _timelineHost.LengthMilliseconds ?? 0,
                    _timelineHost.SeekMilliseconds,
                    () => _timelineHost.TimeMilliseconds ?? 0,
                    _sessionHost.SetCurrentVideoTime)));

    public Task<CodingMoveByCommandResult> MoveNextAsync(string traceName)
        => MoveByCommandAsync(host => host.ExecuteMoveNext(), traceName);

    public Task<CodingMoveByCommandResult> MovePreviousAsync(string traceName)
        => MoveByCommandAsync(host => host.ExecuteMovePrevious(), traceName);

    private double ResolveCurrentDisplayMeter()
        => CodingDisplayMeterResolveWorkflow.Execute(
            new CodingDisplayMeterResolveRequest(_sessionHost.HasViewModel),
            new CodingDisplayMeterResolveActions(
                ResolveDisplayMeter: () => CodingVideoNavigationController.ResolveDisplayMeter(
                    _osdMeterController.LastMeter,
                    _timelineHost.TimeMilliseconds ?? 0,
                    _timelineHost.LengthMilliseconds ?? 0,
                    _sessionHost.EndMeter,
                    _sessionHost.CurrentMeter)))
            .DisplayMeter;

    private Task<CodingMoveByCommandResult> MoveByCommandAsync(
        Action<ICodingSessionHost> executeMoveCommand,
        string traceName)
        => CodingMoveByCommandWorkflow.ExecuteAsync(
            new CodingMoveByCommandRequest(
                _sessionHost.HasViewModel,
                traceName),
            new CodingMoveByCommandActions(
                PrepareMoveByCommand: () => CodingVideoNavigationController.PrepareMoveByCommand(
                    _sessionHost,
                    executeMoveCommand,
                    _pendingState.MarkPending,
                    _actions.PausePlayback,
                    _osdMeterController.ResetRecentMeter),
                ReadOsdMeterAsync: _actions.ReadOsdMeterAsync,
                TraceError: _actions.TraceError));
}
