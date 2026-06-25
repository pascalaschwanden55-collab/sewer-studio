using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingInlineDefectAcceptCommandWorkflowOutcome
{
    NotAccepted,
    Accepted
}

public sealed record CodingInlineDefectAcceptCommandActions(
    Func<CodingEvent?> AcceptDefect,
    Action<CodingEvent> UpdateInlineDefectDetail,
    Action RefreshEvents,
    Action FadeOutAiOverlayAfterAction);

public sealed record CodingInlineDefectAcceptCommandWorkflowResult(
    CodingInlineDefectAcceptCommandWorkflowOutcome Outcome)
{
    public bool Completed => Outcome == CodingInlineDefectAcceptCommandWorkflowOutcome.Accepted;
}

public static class CodingInlineDefectAcceptCommandWorkflow
{
    public static CodingInlineDefectAcceptCommandWorkflowResult Execute(
        CodingInlineDefectAcceptCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var acceptedDefect = actions.AcceptDefect();
        if (acceptedDefect is null)
            return Result(CodingInlineDefectAcceptCommandWorkflowOutcome.NotAccepted);

        actions.UpdateInlineDefectDetail(acceptedDefect);
        actions.RefreshEvents();
        actions.FadeOutAiOverlayAfterAction();
        return Result(CodingInlineDefectAcceptCommandWorkflowOutcome.Accepted);
    }

    private static CodingInlineDefectAcceptCommandWorkflowResult Result(
        CodingInlineDefectAcceptCommandWorkflowOutcome outcome)
        => new(outcome);
}
