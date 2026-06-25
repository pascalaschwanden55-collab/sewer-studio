using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionPulseWorkflowTests
{
    [Fact]
    public void Start_skips_when_pulse_is_already_running()
    {
        var result = LiveDetectionPulseWorkflow.Start(
            new LiveDetectionPulseStartRequest(IsRunning: true),
            new LiveDetectionPulseStartActions(
                SetRunning: () => throw new InvalidOperationException("Running flag should not change."),
                StartPulse: () => throw new InvalidOperationException("Pulse should not start.")));

        Assert.Equal(LiveDetectionPulseWorkflowOutcome.AlreadyRunning, result.Outcome);
        Assert.False(result.Changed);
    }

    [Fact]
    public void Start_sets_running_before_starting_pulse()
    {
        var calls = new List<string>();

        var result = LiveDetectionPulseWorkflow.Start(
            new LiveDetectionPulseStartRequest(IsRunning: false),
            new LiveDetectionPulseStartActions(
                SetRunning: () => calls.Add("running"),
                StartPulse: () => calls.Add("start")));

        Assert.Equal(LiveDetectionPulseWorkflowOutcome.Started, result.Outcome);
        Assert.True(result.Changed);
        Assert.Equal(["running", "start"], calls);
    }

    [Fact]
    public void Stop_clears_running_before_stopping_pulse()
    {
        var calls = new List<string>();

        var result = LiveDetectionPulseWorkflow.Stop(
            new LiveDetectionPulseStopActions(
                ClearRunning: () => calls.Add("clear"),
                StopPulse: () => calls.Add("stop")));

        Assert.Equal(LiveDetectionPulseWorkflowOutcome.Stopped, result.Outcome);
        Assert.True(result.Changed);
        Assert.Equal(["clear", "stop"], calls);
    }
}
