using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionManualMarkTrainingCommandOutcome
{
    SelectionCancelled,
    Saved,
    NotSaved,
    Failed
}

public sealed record LiveDetectionManualMarkTrainingCommandActions(
    Func<ProtocolEntry?> SelectEntry,
    Func<ProtocolEntry, Task<LiveDetectionManualMarkTrainingResult>> SaveTrainingAsync,
    Func<LiveDetectionManualMarkTrainingResult, LiveDetectionManualMarkTrainingResultWorkflowResult> HandleTrainingResult,
    Action<string, bool> ShowOsdMeterStatus);

public sealed record LiveDetectionManualMarkTrainingCommandResult(
    LiveDetectionManualMarkTrainingCommandOutcome Outcome)
{
    public bool Saved => Outcome == LiveDetectionManualMarkTrainingCommandOutcome.Saved;

    public bool ReturnValue => Saved;
}

public static class LiveDetectionManualMarkTrainingCommandWorkflow
{
    public static async Task<LiveDetectionManualMarkTrainingCommandResult> ExecuteAsync(
        LiveDetectionManualMarkTrainingCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        try
        {
            var selectedEntry = actions.SelectEntry();
            if (selectedEntry == null)
                return Result(LiveDetectionManualMarkTrainingCommandOutcome.SelectionCancelled);

            var trainingResult = await actions.SaveTrainingAsync(selectedEntry);
            var handledResult = actions.HandleTrainingResult(trainingResult);
            return Result(handledResult.Saved
                ? LiveDetectionManualMarkTrainingCommandOutcome.Saved
                : LiveDetectionManualMarkTrainingCommandOutcome.NotSaved);
        }
        catch (Exception ex)
        {
            actions.ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", false);
            return Result(LiveDetectionManualMarkTrainingCommandOutcome.Failed);
        }
    }

    private static LiveDetectionManualMarkTrainingCommandResult Result(
        LiveDetectionManualMarkTrainingCommandOutcome outcome)
        => new(outcome);
}
