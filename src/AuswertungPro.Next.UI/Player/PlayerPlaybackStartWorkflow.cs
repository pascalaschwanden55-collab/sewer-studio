namespace AuswertungPro.Next.UI.Player;

public enum PlayerPlaybackStartWorkflowOutcome
{
    Idle,
    Started
}

public sealed record PlayerPlaybackEnsurePlayingRequest(
    bool ShouldStartPlayback,
    string VideoPath);

public sealed record PlayerPlaybackEnsurePlayingActions(
    Action<string> Play);

public sealed record PlayerPlaybackStartRequest(
    string VideoPath);

public sealed record PlayerPlaybackStartActions(
    Action<string> PlayPath,
    Action StartTimer,
    Action UpdateRateLabel);

public sealed record PlayerPlaybackStartWorkflowResult(
    PlayerPlaybackStartWorkflowOutcome Outcome)
{
    public bool Handled => Outcome == PlayerPlaybackStartWorkflowOutcome.Started;
}

public static class PlayerPlaybackStartWorkflow
{
    public static PlayerPlaybackStartWorkflowResult EnsurePlaying(
        PlayerPlaybackEnsurePlayingRequest request,
        PlayerPlaybackEnsurePlayingActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.ShouldStartPlayback)
            return new PlayerPlaybackStartWorkflowResult(PlayerPlaybackStartWorkflowOutcome.Idle);

        actions.Play(request.VideoPath);
        return new PlayerPlaybackStartWorkflowResult(PlayerPlaybackStartWorkflowOutcome.Started);
    }

    public static PlayerPlaybackStartWorkflowResult Play(
        PlayerPlaybackStartRequest request,
        PlayerPlaybackStartActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.PlayPath(request.VideoPath);
        actions.StartTimer();
        actions.UpdateRateLabel();

        return new PlayerPlaybackStartWorkflowResult(PlayerPlaybackStartWorkflowOutcome.Started);
    }
}
