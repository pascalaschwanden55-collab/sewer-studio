using System;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionHideStatusTimerWorkflowOutcome
{
    Scheduled
}

public sealed record LiveDetectionHideStatusTimerWorkflowActions(
    Action<TimeSpan, Action> Schedule,
    Func<bool> IsDetecting,
    Action HideDetectionStatus);

public sealed record LiveDetectionHideStatusTimerWorkflowResult(
    LiveDetectionHideStatusTimerWorkflowOutcome Outcome);

public static class LiveDetectionHideStatusTimerWorkflow
{
    public static LiveDetectionHideStatusTimerWorkflowResult Schedule(
        LiveDetectionHideStatusTimerWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.Schedule(TimeSpan.FromSeconds(5), () =>
        {
            if (!actions.IsDetecting())
                actions.HideDetectionStatus();
        });

        return new LiveDetectionHideStatusTimerWorkflowResult(
            LiveDetectionHideStatusTimerWorkflowOutcome.Scheduled);
    }
}
