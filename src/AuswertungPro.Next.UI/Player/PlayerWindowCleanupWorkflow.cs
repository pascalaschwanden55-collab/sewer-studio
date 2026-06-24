namespace AuswertungPro.Next.UI.Player;

public enum PlayerWindowCleanupWorkflowOutcome
{
    Skipped,
    Cleaned
}

public sealed record PlayerWindowCleanupWorkflowRequest(
    bool IsPlaybackDisposed);

public sealed record PlayerWindowCleanupWorkflowActions(
    Action MarkPlaybackDisposed,
    Action StopPlayerTimers,
    Action DetachVideoView,
    Action DisposeMediaPlayer,
    Action DisposeLibVlc);

public sealed record PlayerWindowCleanupWorkflowResult(
    PlayerWindowCleanupWorkflowOutcome Outcome)
{
    public bool Cleaned => Outcome == PlayerWindowCleanupWorkflowOutcome.Cleaned;
}

public static class PlayerWindowCleanupWorkflow
{
    public static PlayerWindowCleanupWorkflowResult Execute(
        PlayerWindowCleanupWorkflowRequest request,
        PlayerWindowCleanupWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsPlaybackDisposed)
            return new PlayerWindowCleanupWorkflowResult(
                PlayerWindowCleanupWorkflowOutcome.Skipped);

        actions.MarkPlaybackDisposed();
        actions.StopPlayerTimers();
        actions.DetachVideoView();
        actions.DisposeMediaPlayer();
        actions.DisposeLibVlc();

        return new PlayerWindowCleanupWorkflowResult(
            PlayerWindowCleanupWorkflowOutcome.Cleaned);
    }
}
