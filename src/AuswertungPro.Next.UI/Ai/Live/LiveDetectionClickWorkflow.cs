namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionClickWorkflowOutcome
{
    Stopped,
    Started
}

public sealed record LiveDetectionClickWorkflowRequest(bool IsDetecting);

public sealed record LiveDetectionClickWorkflowActions(
    Action StopLiveDetection,
    Action UncheckToggle,
    Func<Task> StartLiveDetectionAsync);

public sealed record LiveDetectionClickWorkflowResult(LiveDetectionClickWorkflowOutcome Outcome);

public static class LiveDetectionClickWorkflow
{
    public static async Task<LiveDetectionClickWorkflowResult> ExecuteAsync(
        LiveDetectionClickWorkflowRequest request,
        LiveDetectionClickWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsDetecting)
        {
            actions.StopLiveDetection();
            actions.UncheckToggle();
            return Result(LiveDetectionClickWorkflowOutcome.Stopped);
        }

        await actions.StartLiveDetectionAsync().ConfigureAwait(true);
        return Result(LiveDetectionClickWorkflowOutcome.Started);
    }

    private static LiveDetectionClickWorkflowResult Result(LiveDetectionClickWorkflowOutcome outcome)
        => new(outcome);
}
