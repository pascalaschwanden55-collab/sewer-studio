using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventDeleteCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_selected_event()
    {
        var result = CodingEventDeleteCommandWorkflow.Execute(
            new CodingEventDeleteCommandRequest(SelectedEvent: null),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingEventDeleteCommandWorkflowOutcome.NoSelection, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_confirms_before_cancelled_delete()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingEventDeleteCommandWorkflow.Execute(
            new CodingEventDeleteCommandRequest(ev),
            Actions(
                calls.Add,
                confirmDelete: code =>
                {
                    calls.Add($"confirm:{code}");
                    return false;
                }));

        Assert.Equal(["confirm:BBA"], calls);
        Assert.Equal(CodingEventDeleteCommandWorkflowOutcome.Cancelled, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_deletes_and_refreshes_without_selected_defect_clear()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingEventDeleteCommandWorkflow.Execute(
            new CodingEventDeleteCommandRequest(ev),
            Actions(
                calls.Add,
                delete: selected =>
                {
                    calls.Add($"delete:{selected.Entry.Code}");
                    return new CodingEventListDeleteResult(
                        Deleted: true,
                        ShouldClearSelectedDefect: false);
                }));

        Assert.Equal(["confirm:BBA", "delete:BBA", "hide-detail", "refresh"], calls);
        Assert.Equal(CodingEventDeleteCommandWorkflowOutcome.Deleted, result.Outcome);
        Assert.True(result.Completed);
    }

    [Fact]
    public void Execute_clears_selected_defect_before_hiding_and_refreshing()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingEventDeleteCommandWorkflow.Execute(
            new CodingEventDeleteCommandRequest(ev),
            Actions(
                calls.Add,
                delete: _ => new CodingEventListDeleteResult(
                    Deleted: true,
                    ShouldClearSelectedDefect: true)));

        Assert.Equal(["confirm:BBA", "clear-selected", "hide-detail", "refresh"], calls);
        Assert.Equal(CodingEventDeleteCommandWorkflowOutcome.Deleted, result.Outcome);
        Assert.True(result.Completed);
    }

    [Fact]
    public void Execute_stops_after_unapplied_delete()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingEventDeleteCommandWorkflow.Execute(
            new CodingEventDeleteCommandRequest(ev),
            Actions(
                calls.Add,
                delete: _ => new CodingEventListDeleteResult(
                    Deleted: false,
                    ShouldClearSelectedDefect: true)));

        Assert.Equal(["confirm:BBA"], calls);
        Assert.Equal(CodingEventDeleteCommandWorkflowOutcome.NotDeleted, result.Outcome);
        Assert.False(result.Completed);
    }

    private static CodingEventDeleteCommandActions Actions(
        Action<string> calls,
        Func<string, bool>? confirmDelete = null,
        Func<CodingEvent, CodingEventListDeleteResult>? delete = null)
        => new(
            ConfirmDelete: confirmDelete ?? (code =>
            {
                calls($"confirm:{code}");
                return true;
            }),
            Delete: delete ?? (_ => new CodingEventListDeleteResult(
                Deleted: true,
                ShouldClearSelectedDefect: false)),
            ClearSelectedDefect: () => calls("clear-selected"),
            HideInlineDefectDetail: () => calls("hide-detail"),
            RefreshEvents: () => calls("refresh"));

    private static CodingEvent Event(string code)
        => new()
        {
            EventId = Guid.NewGuid(),
            Entry = new ProtocolEntry { Code = code }
        };
}
