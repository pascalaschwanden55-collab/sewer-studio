using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingInlineDefectEditCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_view_model()
    {
        var result = CodingInlineDefectEditCommandWorkflow.Execute(
            new CodingInlineDefectEditCommandRequest(
                HasViewModel: false,
                SelectedDefect: Event("BBA"),
                SelectedListEvent: null),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingInlineDefectEditCommandWorkflowOutcome.NoViewModel, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_skips_without_selected_event()
    {
        var result = CodingInlineDefectEditCommandWorkflow.Execute(
            new CodingInlineDefectEditCommandRequest(
                HasViewModel: true,
                SelectedDefect: null,
                SelectedListEvent: null),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingInlineDefectEditCommandWorkflowOutcome.NoSelection, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_prefers_selected_defect_over_list_selection()
    {
        var calls = new List<string>();
        var selectedDefect = Event("DEFECT");
        var listEvent = Event("LIST");

        var result = CodingInlineDefectEditCommandWorkflow.Execute(
            new CodingInlineDefectEditCommandRequest(
                HasViewModel: true,
                SelectedDefect: selectedDefect,
                SelectedListEvent: listEvent),
            Actions(
                calls.Add,
                tryEdit: selected =>
                {
                    calls.Add($"edit:{selected.Entry.Code}");
                    return false;
                }));

        Assert.Equal(
            ["select:DEFECT", "pause", "edit:DEFECT"],
            calls);
        Assert.Equal(CodingInlineDefectEditCommandWorkflowOutcome.EditCancelled, result.Outcome);
    }

    [Fact]
    public void Execute_completes_refreshes_and_updates_detail_after_successful_edit()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingInlineDefectEditCommandWorkflow.Execute(
            new CodingInlineDefectEditCommandRequest(
                HasViewModel: true,
                SelectedDefect: null,
                SelectedListEvent: ev),
            Actions(
                calls.Add,
                tryEdit: selected =>
                {
                    calls.Add($"edit:{selected.Entry.Code}");
                    return true;
                },
                completeEdit: selected =>
                {
                    calls.Add($"complete:{selected.Entry.Code}");
                    return true;
                }));

        Assert.Equal(
            ["select:BBA", "pause", "edit:BBA", "complete:BBA", "refresh", "detail:BBA"],
            calls);
        Assert.Equal(CodingInlineDefectEditCommandWorkflowOutcome.Edited, result.Outcome);
        Assert.True(result.Completed);
    }

    [Fact]
    public void Execute_stops_when_complete_edit_declines_update()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingInlineDefectEditCommandWorkflow.Execute(
            new CodingInlineDefectEditCommandRequest(
                HasViewModel: true,
                SelectedDefect: ev,
                SelectedListEvent: null),
            Actions(
                calls.Add,
                tryEdit: _ => true,
                completeEdit: selected =>
                {
                    calls.Add($"complete:{selected.Entry.Code}");
                    return false;
                }));

        Assert.Equal(
            ["select:BBA", "pause", "complete:BBA"],
            calls);
        Assert.Equal(CodingInlineDefectEditCommandWorkflowOutcome.EditNotCompleted, result.Outcome);
        Assert.False(result.Completed);
    }

    private static CodingInlineDefectEditCommandActions Actions(
        Action<string> calls,
        Func<CodingEvent, bool>? tryEdit = null,
        Func<CodingEvent, bool>? completeEdit = null)
        => new(
            SelectDefect: selected => calls($"select:{selected.Entry.Code}"),
            PausePlayback: () => calls("pause"),
            TryEdit: tryEdit ?? (_ => throw new InvalidOperationException("Edit should not run.")),
            CompleteEdit: completeEdit ?? (_ => throw new InvalidOperationException("Complete should not run.")),
            RefreshEvents: () => calls("refresh"),
            UpdateInlineDefectDetail: selected => calls($"detail:{selected.Entry.Code}"));

    private static CodingEvent Event(string code)
        => new()
        {
            EventId = Guid.NewGuid(),
            Entry = new() { Code = code }
        };
}
