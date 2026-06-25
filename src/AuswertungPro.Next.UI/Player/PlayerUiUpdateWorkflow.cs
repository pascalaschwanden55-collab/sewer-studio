namespace AuswertungPro.Next.UI.Player;

public enum PlayerUiUpdateWorkflowOutcome
{
    Skipped,
    Updated
}

public sealed record PlayerUiUpdateWorkflowRequest(
    bool IsDragging,
    bool IsCodingMode,
    long CurrentTimeMs,
    long DurationMs);

public sealed record PlayerUiUpdateWorkflowActions(
    Action<long, long> ApplyPlaybackState,
    Action UpdateRateLabel,
    Action UpdateCodingCurrentCode);

public sealed record PlayerUiUpdateWorkflowResult(
    PlayerUiUpdateWorkflowOutcome Outcome)
{
    public bool Updated => Outcome == PlayerUiUpdateWorkflowOutcome.Updated;
}

public static class PlayerUiUpdateWorkflow
{
    public static PlayerUiUpdateWorkflowResult Execute(
        PlayerUiUpdateWorkflowRequest request,
        PlayerUiUpdateWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsDragging)
            return new PlayerUiUpdateWorkflowResult(PlayerUiUpdateWorkflowOutcome.Skipped);

        actions.ApplyPlaybackState(request.CurrentTimeMs, request.DurationMs);
        actions.UpdateRateLabel();

        if (request.IsCodingMode)
            actions.UpdateCodingCurrentCode();

        return new PlayerUiUpdateWorkflowResult(PlayerUiUpdateWorkflowOutcome.Updated);
    }
}
