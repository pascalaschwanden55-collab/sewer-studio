using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionTimerDispatchWorkflowTests
{
    [Fact]
    public void Execute_skips_when_window_is_closing()
    {
        var result = LiveDetectionTimerDispatchWorkflow.Execute(
            new LiveDetectionTimerDispatchWorkflowRequest(
                IsClosing: true,
                IsPlaybackDisposed: false),
            NoActions());

        Assert.Equal(LiveDetectionTimerDispatchWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Dispatched);
    }

    [Fact]
    public void Execute_dispatches_detection_task_with_error_logging()
    {
        var calls = new List<string>();

        var result = LiveDetectionTimerDispatchWorkflow.Execute(
            new LiveDetectionTimerDispatchWorkflowRequest(
                IsClosing: false,
                IsPlaybackDisposed: false),
            new LiveDetectionTimerDispatchWorkflowActions(
                RunDetectionAsync: () =>
                {
                    calls.Add("run");
                    return Task.CompletedTask;
                },
                Dispatch: (runDetectionAsync, operationName, onError) =>
                {
                    calls.Add($"dispatch:{operationName}");
                    _ = runDetectionAsync();
                    onError(new InvalidOperationException("boom"));
                },
                LogError: message => calls.Add($"log:{message}")));

        Assert.Equal(
            [
                "dispatch:DetectionTimer",
                "run",
                "log:[PlayerWindow] DetectionTimer_Tick Fehler: boom"
            ],
            calls);
        Assert.Equal(LiveDetectionTimerDispatchWorkflowOutcome.Dispatched, result.Outcome);
        Assert.True(result.Dispatched);
    }

    private static LiveDetectionTimerDispatchWorkflowActions NoActions()
        => new(
            RunDetectionAsync: () => throw new InvalidOperationException("Run should not start."),
            Dispatch: (_, _, _) => throw new InvalidOperationException("Dispatch should not run."),
            LogError: _ => throw new InvalidOperationException("Log should not run."));
}
