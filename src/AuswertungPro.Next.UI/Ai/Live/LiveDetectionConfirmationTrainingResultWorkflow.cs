namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionConfirmationTrainingResultOutcome
{
    NotSaved,
    AcceptedSaved,
    CorrectedSaved
}

public sealed record LiveDetectionConfirmationTrainingResultActions(
    Action<string, bool> ShowOsdMeterStatus,
    Action ResumeDetection);

public sealed record LiveDetectionConfirmationTrainingResultWorkflowResult(
    LiveDetectionConfirmationTrainingResultOutcome Outcome)
{
    public bool Saved => Outcome != LiveDetectionConfirmationTrainingResultOutcome.NotSaved;
}

public static class LiveDetectionConfirmationTrainingResultWorkflow
{
    public static LiveDetectionConfirmationTrainingResultWorkflowResult ExecuteAccepted(
        LiveDetectionConfirmationTrainingResult trainingResult,
        LiveDetectionConfirmationTrainingResultActions actions)
    {
        ArgumentNullException.ThrowIfNull(trainingResult);
        ArgumentNullException.ThrowIfNull(actions);

        if (!trainingResult.Saved)
            return Resume(actions, LiveDetectionConfirmationTrainingResultOutcome.NotSaved);

        actions.ShowOsdMeterStatus($"\u2713 {trainingResult.SavedCount} Befund(e) gespeichert", true);
        return Resume(actions, LiveDetectionConfirmationTrainingResultOutcome.AcceptedSaved);
    }

    public static LiveDetectionConfirmationTrainingResultWorkflowResult ExecuteCorrected(
        LiveDetectionConfirmationTrainingResult trainingResult,
        LiveDetectionConfirmationTrainingResultActions actions)
    {
        ArgumentNullException.ThrowIfNull(trainingResult);
        ArgumentNullException.ThrowIfNull(actions);

        if (!trainingResult.Saved)
            return Resume(actions, LiveDetectionConfirmationTrainingResultOutcome.NotSaved);

        actions.ShowOsdMeterStatus($"\u2713 Training: {trainingResult.Code} (korrigiert)", true);
        return Resume(actions, LiveDetectionConfirmationTrainingResultOutcome.CorrectedSaved);
    }

    private static LiveDetectionConfirmationTrainingResultWorkflowResult Resume(
        LiveDetectionConfirmationTrainingResultActions actions,
        LiveDetectionConfirmationTrainingResultOutcome outcome)
    {
        actions.ResumeDetection();
        return new LiveDetectionConfirmationTrainingResultWorkflowResult(outcome);
    }
}
