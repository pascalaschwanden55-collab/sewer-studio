namespace AuswertungPro.Next.UI.Ai;

public enum CodingInlineDefectRejectCommandWorkflowOutcome
{
    NotRejected,
    Rejected
}

public sealed record CodingInlineDefectRejectCommandActions(
    Func<CodingInlineDefectRejectResult> RejectDefect,
    Action ClearSelectedDefect,
    Action HideInlineDefectDetail,
    Action RefreshEvents,
    Action FadeOutAiOverlayAfterAction);

public sealed record CodingInlineDefectRejectCommandWorkflowResult(
    CodingInlineDefectRejectCommandWorkflowOutcome Outcome)
{
    public bool Completed => Outcome == CodingInlineDefectRejectCommandWorkflowOutcome.Rejected;
}

public static class CodingInlineDefectRejectCommandWorkflow
{
    public static CodingInlineDefectRejectCommandWorkflowResult Execute(
        CodingInlineDefectRejectCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var rejectResult = actions.RejectDefect();
        if (!rejectResult.Rejected)
            return Result(CodingInlineDefectRejectCommandWorkflowOutcome.NotRejected);

        if (rejectResult.ShouldClearSelectedDefect)
            actions.ClearSelectedDefect();

        actions.HideInlineDefectDetail();
        actions.RefreshEvents();
        actions.FadeOutAiOverlayAfterAction();
        return Result(CodingInlineDefectRejectCommandWorkflowOutcome.Rejected);
    }

    private static CodingInlineDefectRejectCommandWorkflowResult Result(
        CodingInlineDefectRejectCommandWorkflowOutcome outcome)
        => new(outcome);
}
