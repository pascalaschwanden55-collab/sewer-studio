namespace AuswertungPro.Next.UI.Ai;

public enum CodingScreenshotToastWorkflowOutcome
{
    Failed,
    Scheduled
}

public sealed record CodingScreenshotToastWorkflowRequest(string Message);

public sealed record CodingScreenshotToastWorkflowActions(
    Action<string> ShowStatusMessage,
    Action<TimeSpan, Action> ScheduleHideStatus,
    Action HideStatus);

public sealed record CodingScreenshotToastWorkflowResult(CodingScreenshotToastWorkflowOutcome Outcome)
{
    public bool HideScheduled => Outcome == CodingScreenshotToastWorkflowOutcome.Scheduled;
}

public static class CodingScreenshotToastWorkflow
{
    public static readonly TimeSpan HideDelay = TimeSpan.FromSeconds(2.5);

    public static CodingScreenshotToastWorkflowResult Show(
        CodingScreenshotToastWorkflowRequest request,
        CodingScreenshotToastWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        try
        {
            actions.ShowStatusMessage(request.Message);
            actions.ScheduleHideStatus(HideDelay, actions.HideStatus);
            return Result(CodingScreenshotToastWorkflowOutcome.Scheduled);
        }
        catch
        {
            return Result(CodingScreenshotToastWorkflowOutcome.Failed);
        }
    }

    private static CodingScreenshotToastWorkflowResult Result(CodingScreenshotToastWorkflowOutcome outcome)
        => new(outcome);
}
