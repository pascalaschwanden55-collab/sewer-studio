namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionPulseWorkflowOutcome
{
    AlreadyRunning,
    Started,
    Stopped
}

public sealed record LiveDetectionPulseStartRequest(bool IsRunning);

public sealed record LiveDetectionPulseStartActions(
    Action SetRunning,
    Action StartPulse);

public sealed record LiveDetectionPulseStopActions(
    Action ClearRunning,
    Action StopPulse);

public sealed record LiveDetectionPulseWorkflowResult(
    LiveDetectionPulseWorkflowOutcome Outcome)
{
    public bool Changed => Outcome is LiveDetectionPulseWorkflowOutcome.Started
        or LiveDetectionPulseWorkflowOutcome.Stopped;
}

public static class LiveDetectionPulseWorkflow
{
    public static LiveDetectionPulseWorkflowResult Start(
        LiveDetectionPulseStartRequest request,
        LiveDetectionPulseStartActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsRunning)
            return Result(LiveDetectionPulseWorkflowOutcome.AlreadyRunning);

        actions.SetRunning();
        actions.StartPulse();
        return Result(LiveDetectionPulseWorkflowOutcome.Started);
    }

    public static LiveDetectionPulseWorkflowResult Stop(
        LiveDetectionPulseStopActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.ClearRunning();
        actions.StopPulse();
        return Result(LiveDetectionPulseWorkflowOutcome.Stopped);
    }

    private static LiveDetectionPulseWorkflowResult Result(
        LiveDetectionPulseWorkflowOutcome outcome)
        => new(outcome);
}
