using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingModePreparePlaybackWorkflowRequest(
    bool IsLiveDetectionRunning);

public sealed record CodingModePreparePlaybackWorkflowActions(
    Action<bool> SetPause,
    Action StopLiveDetection,
    Action UncheckLiveDetectionToggle,
    Action HideLiveDetectionEntry);

public static class CodingModePreparePlaybackWorkflow
{
    public static void Execute(
        CodingModePreparePlaybackWorkflowRequest request,
        CodingModePreparePlaybackWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        PlayerCodingPlayback.PauseForCodingInteraction(actions.SetPause);

        if (request.IsLiveDetectionRunning)
        {
            actions.StopLiveDetection();
            actions.UncheckLiveDetectionToggle();
        }

        actions.HideLiveDetectionEntry();
    }
}
