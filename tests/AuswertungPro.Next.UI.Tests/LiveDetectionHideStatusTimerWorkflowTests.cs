using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionHideStatusTimerWorkflowTests
{
    [Fact]
    public void Schedule_queues_hide_after_five_seconds()
    {
        TimeSpan? scheduledDelay = null;
        Action? scheduledAction = null;

        var result = LiveDetectionHideStatusTimerWorkflow.Schedule(
            new LiveDetectionHideStatusTimerWorkflowActions(
                Schedule: (delay, action) =>
                {
                    scheduledDelay = delay;
                    scheduledAction = action;
                },
                IsDetecting: () => false,
                HideDetectionStatus: () => { }));

        Assert.Equal(TimeSpan.FromSeconds(5), scheduledDelay);
        Assert.NotNull(scheduledAction);
        Assert.Equal(LiveDetectionHideStatusTimerWorkflowOutcome.Scheduled, result.Outcome);
    }

    [Fact]
    public void Scheduled_action_hides_status_when_detection_is_stopped()
    {
        Action? scheduledAction = null;
        var calls = new List<string>();

        LiveDetectionHideStatusTimerWorkflow.Schedule(
            new LiveDetectionHideStatusTimerWorkflowActions(
                Schedule: (_, action) => scheduledAction = action,
                IsDetecting: () => false,
                HideDetectionStatus: () => calls.Add("hide")));

        scheduledAction!();

        Assert.Equal(["hide"], calls);
    }

    [Fact]
    public void Scheduled_action_keeps_status_when_detection_started_again()
    {
        Action? scheduledAction = null;

        LiveDetectionHideStatusTimerWorkflow.Schedule(
            new LiveDetectionHideStatusTimerWorkflowActions(
                Schedule: (_, action) => scheduledAction = action,
                IsDetecting: () => true,
                HideDetectionStatus: () => throw new InvalidOperationException("Hide should not run.")));

        scheduledAction!();
    }
}
