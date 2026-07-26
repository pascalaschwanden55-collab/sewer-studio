using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingEventEditCommandWorkflowOutcome
{
    NoSelection,
    EditCancelled,
    Edited
}

public sealed record CodingEventEditCommandRequest(
    CodingEvent? SelectedEvent);

public sealed record CodingEventEditCommandActions(
    Action PausePlayback,
    Func<CodingEvent, bool> TryEdit,
    Action<CodingEvent> CompleteEdit);

public sealed record CodingEventEditCommandWorkflowResult(
    CodingEventEditCommandWorkflowOutcome Outcome)
{
    public bool Completed => Outcome == CodingEventEditCommandWorkflowOutcome.Edited;
}

public static class CodingEventEditCommandWorkflow
{
    public static CodingEventEditCommandWorkflowResult Execute(
        CodingEventEditCommandRequest request,
        CodingEventEditCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.SelectedEvent is not { } selectedEvent)
            return new CodingEventEditCommandWorkflowResult(
                CodingEventEditCommandWorkflowOutcome.NoSelection);

        actions.PausePlayback();

        if (!actions.TryEdit(selectedEvent))
            return new CodingEventEditCommandWorkflowResult(
                CodingEventEditCommandWorkflowOutcome.EditCancelled);

        actions.CompleteEdit(selectedEvent);
        return new CodingEventEditCommandWorkflowResult(
            CodingEventEditCommandWorkflowOutcome.Edited);
    }
}
