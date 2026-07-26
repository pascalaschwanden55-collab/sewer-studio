namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionManualMarkTrainingResultOutcome
{
    NotSaved,
    Saved
}

public sealed record LiveDetectionManualMarkTrainingResultActions(
    Action<string, bool> ShowOsdMeterStatus);

public sealed record LiveDetectionManualMarkTrainingResultWorkflowResult(
    LiveDetectionManualMarkTrainingResultOutcome Outcome)
{
    public bool Saved => Outcome == LiveDetectionManualMarkTrainingResultOutcome.Saved;

    public bool ReturnValue => Saved;
}

public static class LiveDetectionManualMarkTrainingResultWorkflow
{
    public static LiveDetectionManualMarkTrainingResultWorkflowResult Execute(
        LiveDetectionManualMarkTrainingResult trainingResult,
        LiveDetectionManualMarkTrainingResultActions actions)
    {
        ArgumentNullException.ThrowIfNull(trainingResult);
        ArgumentNullException.ThrowIfNull(actions);

        if (!trainingResult.Saved)
            return Result(LiveDetectionManualMarkTrainingResultOutcome.NotSaved);

        actions.ShowOsdMeterStatus($"\u2713 {trainingResult.Code} gespeichert", true);
        return Result(LiveDetectionManualMarkTrainingResultOutcome.Saved);
    }

    private static LiveDetectionManualMarkTrainingResultWorkflowResult Result(
        LiveDetectionManualMarkTrainingResultOutcome outcome)
        => new(outcome);
}
