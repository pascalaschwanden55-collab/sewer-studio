using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingConfirmationEditCommandWorkflowOutcome
{
    NoSelection,
    Selected
}

public sealed record CodingConfirmationEditCommandActions(
    Func<CodingEvent?> EditConfirmation,
    Action CloseConfirmationPanel,
    Action<CodingEvent> SelectEvent,
    Action ResumeAfterConfirmation);

public sealed record CodingConfirmationEditCommandWorkflowResult(
    CodingConfirmationEditCommandWorkflowOutcome Outcome)
{
    public bool Selected => Outcome == CodingConfirmationEditCommandWorkflowOutcome.Selected;
}

public static class CodingConfirmationEditCommandWorkflow
{
    public static CodingConfirmationEditCommandWorkflowResult Execute(
        CodingConfirmationEditCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var selectedEvent = actions.EditConfirmation();

        actions.CloseConfirmationPanel();

        if (selectedEvent is null)
        {
            actions.ResumeAfterConfirmation();
            return Result(CodingConfirmationEditCommandWorkflowOutcome.NoSelection);
        }

        actions.SelectEvent(selectedEvent);
        actions.ResumeAfterConfirmation();
        return Result(CodingConfirmationEditCommandWorkflowOutcome.Selected);
    }

    private static CodingConfirmationEditCommandWorkflowResult Result(
        CodingConfirmationEditCommandWorkflowOutcome outcome)
        => new(outcome);
}
