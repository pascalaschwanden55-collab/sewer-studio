using System;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingAiOverlayLifecycleWorkflowOutcome
{
    TimerCreated,
    TimerRestarted,
    FadeOutScheduled
}

public sealed record CodingAiOverlayAutoHideRequest(
    bool HasTimer);

public sealed record CodingAiOverlayAutoHideActions(
    Action<TimeSpan, Action> CreateTimer,
    Action StopTimer,
    Action StartTimer,
    Action ClearVisuals);

public sealed record CodingAiOverlayAutoHideHostActions(
    Action<DispatcherTimer> SetTimer,
    Action StopTimer,
    Action StartTimer,
    Action ClearVisuals);

public sealed record CodingAiOverlayFadeOutActions(
    Action RenderAiOverlays,
    Action<TimeSpan, Action> ScheduleClear,
    Action ClearAiOverlays);

public sealed record CodingAiOverlayFadeOutHostActions(
    Action RenderAiOverlays,
    Action ClearAiOverlays);

public sealed record CodingAiOverlayLifecycleWorkflowResult(
    CodingAiOverlayLifecycleWorkflowOutcome Outcome)
{
    public bool CreatedTimer => Outcome == CodingAiOverlayLifecycleWorkflowOutcome.TimerCreated;
}

public static class CodingAiOverlayLifecycleWorkflow
{
    public static CodingAiOverlayLifecycleWorkflowResult ScheduleAutoHide(
        CodingAiOverlayAutoHideRequest request,
        CodingAiOverlayAutoHideHostActions hostActions)
    {
        ArgumentNullException.ThrowIfNull(hostActions);

        return ScheduleAutoHide(
            request,
            new CodingAiOverlayAutoHideActions(
                CreateTimer: (delay, clear) =>
                    hostActions.SetTimer(PlayerWindowTimerFactory.CreateOneShotTimer(delay, clear)),
                StopTimer: hostActions.StopTimer,
                StartTimer: hostActions.StartTimer,
                ClearVisuals: hostActions.ClearVisuals));
    }

    public static CodingAiOverlayLifecycleWorkflowResult ScheduleAutoHide(
        CodingAiOverlayAutoHideRequest request,
        CodingAiOverlayAutoHideActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasTimer)
        {
            actions.CreateTimer(TimeSpan.FromSeconds(3), actions.ClearVisuals);
            actions.StopTimer();
            actions.StartTimer();
            return new CodingAiOverlayLifecycleWorkflowResult(
                CodingAiOverlayLifecycleWorkflowOutcome.TimerCreated);
        }

        actions.StopTimer();
        actions.StartTimer();
        return new CodingAiOverlayLifecycleWorkflowResult(
            CodingAiOverlayLifecycleWorkflowOutcome.TimerRestarted);
    }

    public static CodingAiOverlayLifecycleWorkflowResult FadeOutAfterAction(
        CodingAiOverlayFadeOutHostActions hostActions)
    {
        ArgumentNullException.ThrowIfNull(hostActions);

        return FadeOutAfterAction(
            new CodingAiOverlayFadeOutActions(
                RenderAiOverlays: hostActions.RenderAiOverlays,
                ScheduleClear: (delay, clear) =>
                {
                    var timer = PlayerWindowTimerFactory.CreateOneShotTimer(delay, clear);
                    timer.Start();
                },
                ClearAiOverlays: hostActions.ClearAiOverlays));
    }

    public static CodingAiOverlayLifecycleWorkflowResult FadeOutAfterAction(
        CodingAiOverlayFadeOutActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.RenderAiOverlays();
        actions.ScheduleClear(TimeSpan.FromMilliseconds(800), actions.ClearAiOverlays);

        return new CodingAiOverlayLifecycleWorkflowResult(
            CodingAiOverlayLifecycleWorkflowOutcome.FadeOutScheduled);
    }
}
