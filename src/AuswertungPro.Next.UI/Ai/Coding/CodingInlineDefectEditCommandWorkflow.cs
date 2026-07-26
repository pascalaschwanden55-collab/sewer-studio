using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingInlineDefectEditCommandWorkflowOutcome
{
    NoViewModel,
    NoSelection,
    EditCancelled,
    EditNotCompleted,
    Edited,
    PersistenceFailed
}

public sealed record CodingInlineDefectEditCommandRequest(
    bool HasViewModel,
    CodingEvent? SelectedDefect,
    CodingEvent? SelectedListEvent);

public sealed record CodingInlineDefectEditCommandActions(
    Action<CodingEvent> SelectDefect,
    Action PausePlayback,
    Func<CodingEvent, bool> TryEdit,
    Func<CodingEvent, bool> CompleteEdit,
    Action RefreshEvents,
    Action<CodingEvent> UpdateInlineDefectDetail);

public sealed record CodingInlineDefectEditCommandWorkflowResult(
    CodingInlineDefectEditCommandWorkflowOutcome Outcome,
    string? Error = null)
{
    public bool Completed => Outcome == CodingInlineDefectEditCommandWorkflowOutcome.Edited;
}

public static class CodingInlineDefectEditCommandWorkflow
{
    public static CodingInlineDefectEditCommandWorkflowResult Execute(
        CodingInlineDefectEditCommandRequest request,
        CodingInlineDefectEditCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasViewModel)
            return Result(CodingInlineDefectEditCommandWorkflowOutcome.NoViewModel);

        var selected = request.SelectedDefect ?? request.SelectedListEvent;
        if (selected == null)
            return Result(CodingInlineDefectEditCommandWorkflowOutcome.NoSelection);

        actions.SelectDefect(selected);
        actions.PausePlayback();

        if (!actions.TryEdit(selected))
            return Result(CodingInlineDefectEditCommandWorkflowOutcome.EditCancelled);

        if (!actions.CompleteEdit(selected))
            return Result(CodingInlineDefectEditCommandWorkflowOutcome.EditNotCompleted);

        actions.RefreshEvents();
        actions.UpdateInlineDefectDetail(selected);
        return Result(CodingInlineDefectEditCommandWorkflowOutcome.Edited);
    }

    private static CodingInlineDefectEditCommandWorkflowResult Result(
        CodingInlineDefectEditCommandWorkflowOutcome outcome)
        => new(outcome);
}
