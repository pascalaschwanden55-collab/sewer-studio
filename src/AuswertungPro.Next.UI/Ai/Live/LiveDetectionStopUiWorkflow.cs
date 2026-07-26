using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionStopUiWorkflowOutcome
{
    Skipped,
    Updated
}

public sealed record LiveDetectionStopUiWorkflowRequest(
    bool ShouldUpdateUi,
    bool HideOverlay,
    int TotalEvents,
    bool HasPlayer,
    bool IsPlaybackDisposed,
    bool IsPlayerPlaying);

public sealed record LiveDetectionStopUiWorkflowActions(
    Action SetStoppedStatus,
    Action<bool> ClearOverlay,
    Action<int> ShowStoppedDetectionStatus,
    Action<bool> SetPause,
    Action StartHideStatusTimer);

public sealed record LiveDetectionStopUiWorkflowResult(
    LiveDetectionStopUiWorkflowOutcome Outcome)
{
    public bool UpdatedUi => Outcome == LiveDetectionStopUiWorkflowOutcome.Updated;
}

public static class LiveDetectionStopUiWorkflow
{
    public static LiveDetectionStopUiWorkflowResult Execute(
        LiveDetectionStopUiWorkflowRequest request,
        LiveDetectionStopUiWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.ShouldUpdateUi)
            return new LiveDetectionStopUiWorkflowResult(
                LiveDetectionStopUiWorkflowOutcome.Skipped);

        actions.SetStoppedStatus();
        actions.ClearOverlay(request.HideOverlay);
        actions.ShowStoppedDetectionStatus(request.TotalEvents);
        PlayerLiveDetectionStopPlayback.PauseIfRunning(
            request.HasPlayer,
            request.IsPlaybackDisposed,
            request.IsPlayerPlaying,
            actions.SetPause);
        actions.StartHideStatusTimer();

        return new LiveDetectionStopUiWorkflowResult(
            LiveDetectionStopUiWorkflowOutcome.Updated);
    }
}
