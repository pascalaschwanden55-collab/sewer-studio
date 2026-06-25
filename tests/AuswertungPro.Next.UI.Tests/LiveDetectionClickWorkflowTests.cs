using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionClickWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_stops_and_unchecks_when_detection_is_running()
    {
        var calls = new List<string>();

        var result = await LiveDetectionClickWorkflow.ExecuteAsync(
            new LiveDetectionClickWorkflowRequest(IsDetecting: true),
            new LiveDetectionClickWorkflowActions(
                StopLiveDetection: () => calls.Add("stop"),
                UncheckToggle: () => calls.Add("uncheck"),
                StartLiveDetectionAsync: () => throw new InvalidOperationException("Start must not run.")));

        Assert.Equal(LiveDetectionClickWorkflowOutcome.Stopped, result.Outcome);
        Assert.Equal(["stop", "uncheck"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_starts_when_detection_is_not_running()
    {
        var calls = new List<string>();

        var result = await LiveDetectionClickWorkflow.ExecuteAsync(
            new LiveDetectionClickWorkflowRequest(IsDetecting: false),
            new LiveDetectionClickWorkflowActions(
                StopLiveDetection: () => throw new InvalidOperationException("Stop must not run."),
                UncheckToggle: () => throw new InvalidOperationException("Uncheck must not run."),
                StartLiveDetectionAsync: () =>
                {
                    calls.Add("start");
                    return Task.CompletedTask;
                }));

        Assert.Equal(LiveDetectionClickWorkflowOutcome.Started, result.Outcome);
        Assert.Equal(["start"], calls);
    }
}
