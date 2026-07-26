namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionConfirmationAcceptCommandOutcome
{
    NoPendingFindings,
    AcceptedHandled,
    Failed
}

public sealed record LiveDetectionConfirmationAcceptCommandRequest(
    bool HasPendingFindings);

public sealed record LiveDetectionConfirmationAcceptCommandActions(
    Func<Task<LiveDetectionConfirmationTrainingResult>> SaveAcceptedAsync,
    Action<LiveDetectionConfirmationTrainingResult> HandleAcceptedResult,
    Action<string, bool> ShowOsdMeterStatus,
    Action ResumeDetection);

public sealed record LiveDetectionConfirmationAcceptCommandResult(
    LiveDetectionConfirmationAcceptCommandOutcome Outcome)
{
    public bool Handled => Outcome == LiveDetectionConfirmationAcceptCommandOutcome.AcceptedHandled;
}

public static class LiveDetectionConfirmationAcceptCommandWorkflow
{
    public static async Task<LiveDetectionConfirmationAcceptCommandResult> ExecuteAsync(
        LiveDetectionConfirmationAcceptCommandRequest request,
        LiveDetectionConfirmationAcceptCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasPendingFindings)
        {
            actions.ResumeDetection();
            return Result(LiveDetectionConfirmationAcceptCommandOutcome.NoPendingFindings);
        }

        try
        {
            var trainingResult = await actions.SaveAcceptedAsync();
            actions.HandleAcceptedResult(trainingResult);
            return Result(LiveDetectionConfirmationAcceptCommandOutcome.AcceptedHandled);
        }
        catch (Exception ex)
        {
            actions.ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", false);
        }

        actions.ResumeDetection();
        return Result(LiveDetectionConfirmationAcceptCommandOutcome.Failed);
    }

    private static LiveDetectionConfirmationAcceptCommandResult Result(
        LiveDetectionConfirmationAcceptCommandOutcome outcome)
        => new(outcome);
}
