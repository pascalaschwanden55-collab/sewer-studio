namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionTimerDispatchWorkflowOutcome
{
    Skipped,
    Dispatched
}

public sealed record LiveDetectionTimerDispatchWorkflowRequest(
    bool IsClosing,
    bool IsPlaybackDisposed);

public sealed record LiveDetectionTimerDispatchWorkflowActions(
    Func<Task> RunDetectionAsync,
    Action<Func<Task>, string, Action<Exception>> Dispatch,
    Action<string> LogError);

public sealed record LiveDetectionTimerDispatchWorkflowResult(
    LiveDetectionTimerDispatchWorkflowOutcome Outcome)
{
    public bool Dispatched => Outcome == LiveDetectionTimerDispatchWorkflowOutcome.Dispatched;
}

public static class LiveDetectionTimerDispatchWorkflow
{
    private const string OperationName = "DetectionTimer";

    public static LiveDetectionTimerDispatchWorkflowResult Execute(
        LiveDetectionTimerDispatchWorkflowRequest request,
        LiveDetectionTimerDispatchWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsClosing || request.IsPlaybackDisposed)
            return new LiveDetectionTimerDispatchWorkflowResult(
                LiveDetectionTimerDispatchWorkflowOutcome.Skipped);

        actions.Dispatch(
            actions.RunDetectionAsync,
            OperationName,
            ex => actions.LogError($"[PlayerWindow] DetectionTimer_Tick Fehler: {ex.Message}"));
        return new LiveDetectionTimerDispatchWorkflowResult(
            LiveDetectionTimerDispatchWorkflowOutcome.Dispatched);
    }
}
