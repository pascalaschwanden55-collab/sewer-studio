using System;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerLastOpenedPlaybackWorkflowOutcome
{
    MissingWindow,
    CurrentTimeRead,
    CurrentTimeUnavailable,
    Seeked,
    SeekFailed
}

public sealed record PlayerLastOpenedCurrentTimeRequest(
    bool HasWindow);

public sealed record PlayerLastOpenedCurrentTimeActions(
    Func<PlayerLastOpenedCurrentTimeActionResult> TryGetCurrentTime);

public sealed record PlayerLastOpenedCurrentTimeActionResult(
    bool Success,
    TimeSpan Time);

public sealed record PlayerLastOpenedSeekRequest(
    bool HasWindow,
    TimeSpan Time);

public sealed record PlayerLastOpenedSeekActions(
    Func<TimeSpan, bool> TrySeekTo);

public sealed record PlayerLastOpenedCurrentTimeResult(
    PlayerLastOpenedPlaybackWorkflowOutcome Outcome,
    TimeSpan Time = default)
{
    public bool Success => Outcome == PlayerLastOpenedPlaybackWorkflowOutcome.CurrentTimeRead;
}

public sealed record PlayerLastOpenedSeekResult(
    PlayerLastOpenedPlaybackWorkflowOutcome Outcome)
{
    public bool Success => Outcome == PlayerLastOpenedPlaybackWorkflowOutcome.Seeked;
}

public static class PlayerLastOpenedPlaybackWorkflow
{
    public static PlayerLastOpenedCurrentTimeResult TryGetCurrentTime(
        PlayerLastOpenedCurrentTimeRequest request,
        PlayerLastOpenedCurrentTimeActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasWindow)
            return new PlayerLastOpenedCurrentTimeResult(
                PlayerLastOpenedPlaybackWorkflowOutcome.MissingWindow);

        var result = actions.TryGetCurrentTime();
        return result.Success
            ? new PlayerLastOpenedCurrentTimeResult(
                PlayerLastOpenedPlaybackWorkflowOutcome.CurrentTimeRead,
                result.Time)
            : new PlayerLastOpenedCurrentTimeResult(
                PlayerLastOpenedPlaybackWorkflowOutcome.CurrentTimeUnavailable);
    }

    public static PlayerLastOpenedSeekResult TrySeekTo(
        PlayerLastOpenedSeekRequest request,
        PlayerLastOpenedSeekActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasWindow)
            return new PlayerLastOpenedSeekResult(
                PlayerLastOpenedPlaybackWorkflowOutcome.MissingWindow);

        return actions.TrySeekTo(request.Time)
            ? new PlayerLastOpenedSeekResult(PlayerLastOpenedPlaybackWorkflowOutcome.Seeked)
            : new PlayerLastOpenedSeekResult(PlayerLastOpenedPlaybackWorkflowOutcome.SeekFailed);
    }
}
