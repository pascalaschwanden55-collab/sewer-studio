using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionCodingAiStateWorkflowTests
{
    [Fact]
    public void Execute_shows_state_then_starts_pulse_when_requested()
    {
        var calls = new List<string>();

        var result = LiveDetectionCodingAiStateWorkflow.Execute(
            new LiveDetectionCodingAiStateWorkflowRequest(Pulse: true),
            Actions(calls));

        Assert.Equal(LiveDetectionCodingAiStateWorkflowOutcome.PulseStarted, result.Outcome);
        Assert.Equal(["show", "start-pulse"], calls);
    }

    [Fact]
    public void Execute_shows_state_then_stops_pulse_when_not_requested()
    {
        var calls = new List<string>();

        var result = LiveDetectionCodingAiStateWorkflow.Execute(
            new LiveDetectionCodingAiStateWorkflowRequest(Pulse: false),
            Actions(calls));

        Assert.Equal(LiveDetectionCodingAiStateWorkflowOutcome.PulseStopped, result.Outcome);
        Assert.Equal(["show", "stop-pulse"], calls);
    }

    private static LiveDetectionCodingAiStateWorkflowActions Actions(List<string> calls)
        => new(
            ShowCodingAiState: () => calls.Add("show"),
            StartPulse: () => calls.Add("start-pulse"),
            StopPulse: () => calls.Add("stop-pulse"));
}
