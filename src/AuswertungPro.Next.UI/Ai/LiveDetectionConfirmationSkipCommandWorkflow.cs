namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionConfirmationSkipCommandOutcome
{
    Skipped
}

public sealed record LiveDetectionConfirmationSkipCommandActions(
    Action ResumeDetection);

public sealed record LiveDetectionConfirmationSkipCommandResult(
    LiveDetectionConfirmationSkipCommandOutcome Outcome)
{
    public bool Handled => Outcome == LiveDetectionConfirmationSkipCommandOutcome.Skipped;
}

public static class LiveDetectionConfirmationSkipCommandWorkflow
{
    public static LiveDetectionConfirmationSkipCommandResult Execute(
        LiveDetectionConfirmationSkipCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.ResumeDetection();
        return new LiveDetectionConfirmationSkipCommandResult(
            LiveDetectionConfirmationSkipCommandOutcome.Skipped);
    }
}
