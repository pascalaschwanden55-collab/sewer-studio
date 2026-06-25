using System;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerSnapshotWorkflowOutcome
{
    Skipped,
    Captured,
    Failed
}

public sealed record PlayerSnapshotRequest(
    bool HasPlayerWindow,
    bool IsClosing,
    bool IsPlaybackDisposed,
    bool IsPlaying,
    TimeSpan? CurrentTime);

public sealed record PlayerSnapshotActions(
    Func<PlayerSnapshotCaptureResult> Capture);

public sealed record PlayerSnapshotCaptureResult(
    bool Captured,
    string SnapshotPath);

public sealed record PlayerSnapshotSafeRequest(
    bool IsClosing,
    bool IsPlaybackDisposed);

public sealed record PlayerSnapshotSafeActions(
    Func<bool> PauseIfPlaying,
    Func<bool> IsPlaybackUnavailable,
    Action DisableMarqueeOverlay,
    Func<bool> TakeSnapshot,
    Action<bool> ResumeIfNeeded);

public sealed record PlayerSnapshotWorkflowResult(
    PlayerSnapshotWorkflowOutcome Outcome,
    string SnapshotPath = "")
{
    public bool Captured => Outcome == PlayerSnapshotWorkflowOutcome.Captured;
}

public static class PlayerSnapshotWorkflow
{
    public static PlayerSnapshotWorkflowResult TryTakeSnapshot(
        PlayerSnapshotRequest request,
        PlayerSnapshotActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasPlayerWindow || request.IsClosing || request.IsPlaybackDisposed)
            return new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Skipped);

        if (!request.IsPlaying && (!request.CurrentTime.HasValue || request.CurrentTime.Value <= TimeSpan.Zero))
            return new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Skipped);

        try
        {
            var capture = actions.Capture();
            return capture.Captured
                ? new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Captured, capture.SnapshotPath)
                : new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Failed, capture.SnapshotPath);
        }
        catch
        {
            return new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Failed);
        }
    }

    public static PlayerSnapshotWorkflowResult TakeSnapshotSafe(
        PlayerSnapshotSafeRequest request,
        PlayerSnapshotSafeActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsClosing || request.IsPlaybackDisposed)
            return new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Skipped);

        var wasPlaying = false;
        try
        {
            wasPlaying = actions.PauseIfPlaying();

            if (actions.IsPlaybackUnavailable())
                return new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Skipped);

            actions.DisableMarqueeOverlay();

            return actions.TakeSnapshot()
                ? new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Captured)
                : new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Failed);
        }
        catch
        {
            return new PlayerSnapshotWorkflowResult(PlayerSnapshotWorkflowOutcome.Failed);
        }
        finally
        {
            actions.ResumeIfNeeded(wasPlaying);
        }
    }
}
