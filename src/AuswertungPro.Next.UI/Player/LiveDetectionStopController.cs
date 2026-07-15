using System;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public interface ILiveDetectionStopController
{
    void Stop();
}

public sealed record LiveDetectionStopControllerSources(
    Action StopRuntime,
    Func<bool> ShouldUpdateUi,
    Func<bool> HideOverlay,
    Func<int> GetTotalEvents,
    Func<bool> HasPlayer,
    Func<bool> IsPlaybackDisposed,
    Func<bool> IsPlayerPlaying,
    Func<bool> IsDetecting);

public sealed record LiveDetectionStopControllerActions(
    Action SetStoppedStatus,
    Action<bool> ClearOverlay,
    Action<int> ShowStoppedDetectionStatus,
    Action<bool> SetPause,
    Action<LiveDetectionHideStatusTimerDisplayActions> ScheduleHideStatusTimer,
    Action HideDetectionStatus);

public sealed class LiveDetectionStopController : ILiveDetectionStopController
{
    private readonly LiveDetectionStopControllerSources _sources;
    private readonly LiveDetectionStopControllerActions _actions;

    public LiveDetectionStopController(
        LiveDetectionStopControllerSources sources,
        LiveDetectionStopControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(actions);

        _sources = sources;
        _actions = actions;
    }

    public void Stop()
    {
        _sources.StopRuntime();

        LiveDetectionStopUiWorkflow.Execute(
            new LiveDetectionStopUiWorkflowRequest(
                ShouldUpdateUi: _sources.ShouldUpdateUi(),
                HideOverlay: _sources.HideOverlay(),
                TotalEvents: _sources.GetTotalEvents(),
                HasPlayer: _sources.HasPlayer(),
                IsPlaybackDisposed: _sources.IsPlaybackDisposed(),
                IsPlayerPlaying: _sources.IsPlayerPlaying()),
            new LiveDetectionStopUiWorkflowActions(
                _actions.SetStoppedStatus,
                _actions.ClearOverlay,
                _actions.ShowStoppedDetectionStatus,
                _actions.SetPause,
                StartHideStatusTimer));
    }

    private void StartHideStatusTimer()
        => _actions.ScheduleHideStatusTimer(
            new LiveDetectionHideStatusTimerDisplayActions(
                _sources.IsDetecting,
                _actions.HideDetectionStatus));
}
