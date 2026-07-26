using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionOsdMeterStatusWorkflowTests
{
    [Fact]
    public void Show_displays_message_without_reset_timer()
    {
        var calls = new List<string>();

        var result = LiveDetectionOsdMeterStatusWorkflow.Show(
            new LiveDetectionOsdMeterStatusWorkflowRequest(
                Message: "gespeichert",
                ResetAfterDelay: false),
            new LiveDetectionOsdMeterStatusWorkflowActions(
                ShowMessage: message => calls.Add($"message:{message}"),
                ScheduleReset: (_, _) => throw new InvalidOperationException("Reset should not be scheduled."),
                GetLastMeter: () => throw new InvalidOperationException("Meter should not be read."),
                ShowMeter: _ => throw new InvalidOperationException("Meter should not be shown."),
                HideBadge: () => throw new InvalidOperationException("Badge should not be hidden.")));

        Assert.Equal(["message:gespeichert"], calls);
        Assert.Equal(LiveDetectionOsdMeterStatusWorkflowOutcome.Shown, result.Outcome);
        Assert.False(result.ScheduledReset);
    }

    [Fact]
    public void Show_schedules_meter_restore_after_three_seconds()
    {
        var calls = new List<string>();
        Action? resetAction = null;

        var result = LiveDetectionOsdMeterStatusWorkflow.Show(
            new LiveDetectionOsdMeterStatusWorkflowRequest(
                Message: "gespeichert",
                ResetAfterDelay: true),
            new LiveDetectionOsdMeterStatusWorkflowActions(
                ShowMessage: message => calls.Add($"message:{message}"),
                ScheduleReset: (delay, action) =>
                {
                    calls.Add($"schedule:{delay.TotalSeconds:0}");
                    resetAction = action;
                },
                GetLastMeter: () => 12.34,
                ShowMeter: meter => calls.Add($"meter:{meter:0.00}"),
                HideBadge: () => calls.Add("hide")));

        resetAction!();

        Assert.Equal(["message:gespeichert", "schedule:3", "meter:12.34"], calls);
        Assert.Equal(LiveDetectionOsdMeterStatusWorkflowOutcome.ScheduledReset, result.Outcome);
        Assert.True(result.ScheduledReset);
    }

    [Fact]
    public void Scheduled_reset_hides_badge_when_meter_is_missing()
    {
        Action? resetAction = null;
        var calls = new List<string>();

        LiveDetectionOsdMeterStatusWorkflow.Show(
            new LiveDetectionOsdMeterStatusWorkflowRequest(
                Message: "gespeichert",
                ResetAfterDelay: true),
            new LiveDetectionOsdMeterStatusWorkflowActions(
                ShowMessage: _ => { },
                ScheduleReset: (_, action) => resetAction = action,
                GetLastMeter: () => null,
                ShowMeter: _ => throw new InvalidOperationException("Meter should not be shown."),
                HideBadge: () => calls.Add("hide")));

        resetAction!();

        Assert.Equal(["hide"], calls);
    }
}
