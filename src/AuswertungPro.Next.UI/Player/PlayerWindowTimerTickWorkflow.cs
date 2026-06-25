namespace AuswertungPro.Next.UI.Player;

public enum PlayerWindowTimerTickWorkflowOutcome
{
    Skipped,
    Idle,
    Updated,
    Scrubbed
}

public sealed record PlayerWindowTimerTickWorkflowRequest(
    bool IsClosing,
    bool IsPlaybackDisposed,
    bool IsDragging);

public sealed record PlayerWindowTimerTickWorkflowActions(
    Action UpdateUi,
    Action ScrubSeekToSlider);

public sealed record PlayerWindowTimerTickWorkflowResult(
    PlayerWindowTimerTickWorkflowOutcome Outcome)
{
    public bool Handled =>
        Outcome is PlayerWindowTimerTickWorkflowOutcome.Updated
            or PlayerWindowTimerTickWorkflowOutcome.Scrubbed;
}

public static class PlayerWindowTimerTickWorkflow
{
    public static PlayerWindowTimerTickWorkflowResult ExecuteUpdate(
        PlayerWindowTimerTickWorkflowRequest request,
        PlayerWindowTimerTickWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (ShouldSkip(request))
            return new PlayerWindowTimerTickWorkflowResult(PlayerWindowTimerTickWorkflowOutcome.Skipped);

        actions.UpdateUi();
        return new PlayerWindowTimerTickWorkflowResult(PlayerWindowTimerTickWorkflowOutcome.Updated);
    }

    public static PlayerWindowTimerTickWorkflowResult ExecuteScrub(
        PlayerWindowTimerTickWorkflowRequest request,
        PlayerWindowTimerTickWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (ShouldSkip(request))
            return new PlayerWindowTimerTickWorkflowResult(PlayerWindowTimerTickWorkflowOutcome.Skipped);

        if (!request.IsDragging)
            return new PlayerWindowTimerTickWorkflowResult(PlayerWindowTimerTickWorkflowOutcome.Idle);

        actions.ScrubSeekToSlider();
        return new PlayerWindowTimerTickWorkflowResult(PlayerWindowTimerTickWorkflowOutcome.Scrubbed);
    }

    private static bool ShouldSkip(PlayerWindowTimerTickWorkflowRequest request)
        => request.IsClosing || request.IsPlaybackDisposed;
}
