using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionConfirmationCorrectCommandOutcome
{
    NoPendingFindings,
    SelectionCancelled,
    CorrectedHandled,
    Failed
}

public sealed record LiveDetectionConfirmationCorrectCommandRequest(
    bool HasPendingFindings);

public sealed record LiveDetectionConfirmationCorrectCommandActions(
    Func<ProtocolEntry?> SelectCorrection,
    Func<ProtocolEntry, Task<LiveDetectionConfirmationTrainingResult>> SaveCorrectedAsync,
    Action<LiveDetectionConfirmationTrainingResult> HandleCorrectedResult,
    Action<string, bool> ShowOsdMeterStatus,
    Action ResumeDetection);

public sealed record LiveDetectionConfirmationCorrectCommandResult(
    LiveDetectionConfirmationCorrectCommandOutcome Outcome)
{
    public bool Handled => Outcome == LiveDetectionConfirmationCorrectCommandOutcome.CorrectedHandled;
}

public static class LiveDetectionConfirmationCorrectCommandWorkflow
{
    public static async Task<LiveDetectionConfirmationCorrectCommandResult> ExecuteAsync(
        LiveDetectionConfirmationCorrectCommandRequest request,
        LiveDetectionConfirmationCorrectCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasPendingFindings)
        {
            actions.ResumeDetection();
            return Result(LiveDetectionConfirmationCorrectCommandOutcome.NoPendingFindings);
        }

        try
        {
            var selectedEntry = actions.SelectCorrection();
            if (selectedEntry is null)
            {
                actions.ResumeDetection();
                return Result(LiveDetectionConfirmationCorrectCommandOutcome.SelectionCancelled);
            }

            var trainingResult = await actions.SaveCorrectedAsync(selectedEntry);
            actions.HandleCorrectedResult(trainingResult);
            return Result(LiveDetectionConfirmationCorrectCommandOutcome.CorrectedHandled);
        }
        catch (Exception ex)
        {
            actions.ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", false);
        }

        actions.ResumeDetection();
        return Result(LiveDetectionConfirmationCorrectCommandOutcome.Failed);
    }

    private static LiveDetectionConfirmationCorrectCommandResult Result(
        LiveDetectionConfirmationCorrectCommandOutcome outcome)
        => new(outcome);
}
