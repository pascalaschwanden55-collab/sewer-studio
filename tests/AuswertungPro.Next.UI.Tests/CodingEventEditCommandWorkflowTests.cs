using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventEditCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_selected_event()
    {
        var result = CodingEventEditCommandWorkflow.Execute(
            new CodingEventEditCommandRequest(SelectedEvent: null),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingEventEditCommandWorkflowOutcome.NoSelection, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_pauses_and_runs_edit_dialog_before_cancelled_result()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingEventEditCommandWorkflow.Execute(
            new CodingEventEditCommandRequest(ev),
            Actions(
                calls.Add,
                tryEdit: selected =>
                {
                    calls.Add($"edit:{selected.Entry.Code}");
                    return false;
                }));

        Assert.Equal(["pause", "edit:BBA"], calls);
        Assert.Equal(CodingEventEditCommandWorkflowOutcome.EditCancelled, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_completes_edit_after_successful_dialog()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingEventEditCommandWorkflow.Execute(
            new CodingEventEditCommandRequest(ev),
            Actions(
                calls.Add,
                tryEdit: selected =>
                {
                    calls.Add($"edit:{selected.Entry.Code}");
                    return true;
                },
                completeEdit: selected => calls.Add($"complete:{selected.Entry.Code}")));

        Assert.Equal(["pause", "edit:BBA", "complete:BBA"], calls);
        Assert.Equal(CodingEventEditCommandWorkflowOutcome.Edited, result.Outcome);
        Assert.True(result.Completed);
    }

    private static CodingEventEditCommandActions Actions(
        Action<string> calls,
        Func<CodingEvent, bool>? tryEdit = null,
        Action<CodingEvent>? completeEdit = null)
        => new(
            PausePlayback: () => calls("pause"),
            TryEdit: tryEdit ?? (_ => throw new InvalidOperationException("Edit should not run.")),
            CompleteEdit: completeEdit ?? (_ => throw new InvalidOperationException("Complete should not run.")));

    private static CodingEvent Event(string code)
        => new()
        {
            EventId = Guid.NewGuid(),
            Entry = new() { Code = code }
        };
}
