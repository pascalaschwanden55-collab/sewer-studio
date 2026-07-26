using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionConfirmationDisplayWorkflowOutcome
{
    Ignored,
    Shown,
    Resumed
}

public sealed record LiveDetectionConfirmationShowRequest(
    IReadOnlyList<LiveFrameFinding> Findings,
    bool IsPlaybackDisposed,
    bool IsPlayerPlaying,
    double? TimestampSeconds);

public sealed record LiveDetectionConfirmationShowActions(
    Action<bool> SetPause,
    Action<long> SeekMilliseconds,
    Action<IReadOnlyList<LiveFrameFinding>> ShowConfirmation);

public sealed record LiveDetectionConfirmationResumeRequest(
    bool IsPlayerPlaying);

public sealed record LiveDetectionConfirmationResumeActions(
    Action ClearBuffer,
    Action HideConfirmation,
    Action Play);

public sealed record LiveDetectionConfirmationDisplayWorkflowResult(
    LiveDetectionConfirmationDisplayWorkflowOutcome Outcome)
{
    public bool Handled => Outcome != LiveDetectionConfirmationDisplayWorkflowOutcome.Ignored;
}

public static class LiveDetectionConfirmationDisplayWorkflow
{
    public static LiveDetectionConfirmationDisplayWorkflowResult Show(
        LiveDetectionConfirmationShowRequest request,
        LiveDetectionConfirmationShowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Findings);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.Findings.Count == 0)
            return new LiveDetectionConfirmationDisplayWorkflowResult(
                LiveDetectionConfirmationDisplayWorkflowOutcome.Ignored);

        if (!request.IsPlaybackDisposed)
            PlayerConfirmationPlayback.PauseLiveDetectionConfirmation(
                request.IsPlayerPlaying,
                actions.SetPause);

        if (request.TimestampSeconds.HasValue)
            actions.SeekMilliseconds((long)(request.TimestampSeconds.Value * 1000));

        actions.ShowConfirmation(request.Findings);
        return new LiveDetectionConfirmationDisplayWorkflowResult(
            LiveDetectionConfirmationDisplayWorkflowOutcome.Shown);
    }

    public static LiveDetectionConfirmationDisplayWorkflowResult Resume(
        LiveDetectionConfirmationResumeRequest request,
        LiveDetectionConfirmationResumeActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.ClearBuffer();
        actions.HideConfirmation();

        if (!request.IsPlayerPlaying)
            actions.Play();

        return new LiveDetectionConfirmationDisplayWorkflowResult(
            LiveDetectionConfirmationDisplayWorkflowOutcome.Resumed);
    }
}
