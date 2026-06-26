using System;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerOverlayDisplayWorkflowOutcome
{
    Skipped,
    Shown,
    IgnoredError
}

public sealed record PlayerOverlayDisplayWorkflowRequest(
    bool IsPlaybackDisposed,
    string Text,
    TimeSpan Duration);

public sealed record PlayerOverlayDisplayWorkflowActions(
    Action<PlayerMarqueeOverlayState> ShowMarquee,
    Action<TimeSpan, Action> ScheduleDisable,
    Action DisableMarquee);

public sealed record PlayerOverlayDisplayHostActions(
    Action<PlayerMarqueeOverlayState> ShowMarquee,
    Action DisableMarquee);

public sealed record PlayerOverlayDisplayWorkflowResult(
    PlayerOverlayDisplayWorkflowOutcome Outcome)
{
    public bool Handled => Outcome == PlayerOverlayDisplayWorkflowOutcome.Shown;
}

public static class PlayerOverlayDisplayWorkflow
{
    public static PlayerOverlayDisplayWorkflowResult Show(
        PlayerOverlayDisplayWorkflowRequest request,
        PlayerOverlayDisplayHostActions hostActions)
    {
        ArgumentNullException.ThrowIfNull(hostActions);

        return Show(
            request,
            new PlayerOverlayDisplayWorkflowActions(
                ShowMarquee: hostActions.ShowMarquee,
                ScheduleDisable: (disableAfter, disable) =>
                {
                    var timer = PlayerWindowTimerFactory.CreateOneShotTimer(disableAfter, disable);
                    timer.Start();
                },
                DisableMarquee: hostActions.DisableMarquee));
    }

    public static PlayerOverlayDisplayWorkflowResult Show(
        PlayerOverlayDisplayWorkflowRequest request,
        PlayerOverlayDisplayWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsPlaybackDisposed)
            return new PlayerOverlayDisplayWorkflowResult(PlayerOverlayDisplayWorkflowOutcome.Skipped);

        try
        {
            var marquee = PlayerMarqueeOverlayPolicy.BuildShow(request.Text);
            actions.ShowMarquee(marquee);
            actions.ScheduleDisable(request.Duration, actions.DisableMarquee);
            return new PlayerOverlayDisplayWorkflowResult(PlayerOverlayDisplayWorkflowOutcome.Shown);
        }
        catch
        {
            return new PlayerOverlayDisplayWorkflowResult(PlayerOverlayDisplayWorkflowOutcome.IgnoredError);
        }
    }
}
