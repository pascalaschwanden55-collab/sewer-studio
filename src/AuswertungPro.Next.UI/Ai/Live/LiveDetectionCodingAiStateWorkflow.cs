namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionCodingAiStateWorkflowOutcome
{
    PulseStarted,
    PulseStopped
}

public sealed record LiveDetectionCodingAiStateWorkflowRequest(
    bool Pulse);

public sealed record LiveDetectionCodingAiStateWorkflowActions(
    Action ShowCodingAiState,
    Action StartPulse,
    Action StopPulse);

public sealed record LiveDetectionCodingAiStateWorkflowResult(
    LiveDetectionCodingAiStateWorkflowOutcome Outcome);

public static class LiveDetectionCodingAiStateWorkflow
{
    public static LiveDetectionCodingAiStateWorkflowResult Execute(
        LiveDetectionCodingAiStateWorkflowRequest request,
        LiveDetectionCodingAiStateWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.ShowCodingAiState();
        if (request.Pulse)
        {
            actions.StartPulse();
            return Result(LiveDetectionCodingAiStateWorkflowOutcome.PulseStarted);
        }

        actions.StopPulse();
        return Result(LiveDetectionCodingAiStateWorkflowOutcome.PulseStopped);
    }

    private static LiveDetectionCodingAiStateWorkflowResult Result(
        LiveDetectionCodingAiStateWorkflowOutcome outcome)
        => new(outcome);
}
