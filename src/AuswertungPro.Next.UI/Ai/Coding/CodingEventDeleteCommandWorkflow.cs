using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingEventDeleteCommandWorkflowOutcome
{
    NoSelection,
    Cancelled,
    NotDeleted,
    Deleted
}

public sealed record CodingEventDeleteCommandRequest(
    CodingEvent? SelectedEvent);

public sealed record CodingEventDeleteCommandActions(
    Func<string, bool> ConfirmDelete,
    Func<CodingEvent, CodingEventListDeleteResult> Delete,
    Action ClearSelectedDefect,
    Action HideInlineDefectDetail,
    Action RefreshEvents);

public sealed record CodingEventDeleteCommandWorkflowResult(
    CodingEventDeleteCommandWorkflowOutcome Outcome)
{
    public bool Completed => Outcome == CodingEventDeleteCommandWorkflowOutcome.Deleted;
}

public static class CodingEventDeleteCommandWorkflow
{
    public static CodingEventDeleteCommandWorkflowResult Execute(
        CodingEventDeleteCommandRequest request,
        CodingEventDeleteCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.SelectedEvent is not { } selectedEvent)
            return Result(CodingEventDeleteCommandWorkflowOutcome.NoSelection);

        if (!actions.ConfirmDelete(selectedEvent.Entry.Code))
            return Result(CodingEventDeleteCommandWorkflowOutcome.Cancelled);

        var deleteResult = actions.Delete(selectedEvent);
        if (!deleteResult.Deleted)
            return Result(CodingEventDeleteCommandWorkflowOutcome.NotDeleted);

        if (deleteResult.ShouldClearSelectedDefect)
            actions.ClearSelectedDefect();

        actions.HideInlineDefectDetail();
        actions.RefreshEvents();
        return Result(CodingEventDeleteCommandWorkflowOutcome.Deleted);
    }

    private static CodingEventDeleteCommandWorkflowResult Result(
        CodingEventDeleteCommandWorkflowOutcome outcome)
        => new(outcome);
}
