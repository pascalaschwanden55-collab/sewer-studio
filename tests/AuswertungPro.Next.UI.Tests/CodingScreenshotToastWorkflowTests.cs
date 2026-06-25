using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingScreenshotToastWorkflowTests
{
    [Fact]
    public void Show_displays_message_and_schedules_hide_after_delay()
    {
        var calls = new List<string>();
        Action? scheduledHide = null;

        var result = CodingScreenshotToastWorkflow.Show(
            new CodingScreenshotToastWorkflowRequest("copied"),
            new CodingScreenshotToastWorkflowActions(
                ShowStatusMessage: message => calls.Add($"show:{message}"),
                ScheduleHideStatus: (delay, hide) =>
                {
                    calls.Add($"schedule:{delay.TotalSeconds}");
                    scheduledHide = hide;
                },
                HideStatus: () => calls.Add("hide")));

        Assert.Equal(CodingScreenshotToastWorkflowOutcome.Scheduled, result.Outcome);
        Assert.True(result.HideScheduled);
        Assert.Equal(["show:copied", "schedule:2.5"], calls);

        Assert.NotNull(scheduledHide);
        scheduledHide();

        Assert.Equal(["show:copied", "schedule:2.5", "hide"], calls);
    }

    [Fact]
    public void Show_swallows_status_errors_without_scheduling_hide()
    {
        var result = CodingScreenshotToastWorkflow.Show(
            new CodingScreenshotToastWorkflowRequest("copied"),
            new CodingScreenshotToastWorkflowActions(
                ShowStatusMessage: _ => throw new InvalidOperationException("UI unavailable"),
                ScheduleHideStatus: (_, _) => throw new InvalidOperationException("Hide must not be scheduled."),
                HideStatus: () => throw new InvalidOperationException("Hide must not run.")));

        Assert.Equal(CodingScreenshotToastWorkflowOutcome.Failed, result.Outcome);
        Assert.False(result.HideScheduled);
    }
}
