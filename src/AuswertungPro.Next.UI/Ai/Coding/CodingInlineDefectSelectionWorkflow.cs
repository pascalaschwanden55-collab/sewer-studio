using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingInlineDefectSelectionResult(CodingEvent? SelectedEvent);

public enum CodingInlineDefectSelectionOutcome
{
    DetailHidden,
    DetailShown
}

public sealed record CodingInlineDefectSelectionActions(
    Action<CodingEvent?> SetSelectedDefect,
    Action<CodingEvent> UpdateInlineDefectDetail,
    Action HideInlineDefectDetail);

public sealed record CodingInlineDefectSelectionWorkflowResult(
    CodingInlineDefectSelectionOutcome Outcome,
    CodingEvent? SelectedEvent);

public static class CodingInlineDefectSelectionWorkflow
{
    public static CodingInlineDefectSelectionWorkflowResult Execute(
        object? selectedItem,
        CodingInlineDefectSelectionActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var selectedEvent = selectedItem as CodingEvent;
        actions.SetSelectedDefect(selectedEvent);

        if (selectedEvent is not null)
        {
            actions.UpdateInlineDefectDetail(selectedEvent);
            return Result(CodingInlineDefectSelectionOutcome.DetailShown, selectedEvent);
        }

        actions.HideInlineDefectDetail();
        return Result(CodingInlineDefectSelectionOutcome.DetailHidden, selectedEvent);
    }

    public static CodingInlineDefectSelectionResult Apply(
        object? selectedItem,
        Action<CodingEvent?> setSelectedDefect)
    {
        ArgumentNullException.ThrowIfNull(setSelectedDefect);

        var selectedEvent = selectedItem as CodingEvent;
        setSelectedDefect(selectedEvent);
        return new CodingInlineDefectSelectionResult(selectedEvent);
    }

    private static CodingInlineDefectSelectionWorkflowResult Result(
        CodingInlineDefectSelectionOutcome outcome,
        CodingEvent? selectedEvent)
        => new(outcome, selectedEvent);
}
