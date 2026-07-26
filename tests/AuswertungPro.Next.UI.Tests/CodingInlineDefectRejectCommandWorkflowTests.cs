using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingInlineDefectRejectCommandWorkflowTests
{
    [Fact]
    public void Execute_runs_reject_and_stops_when_not_rejected()
    {
        var calls = new List<string>();

        var result = CodingInlineDefectRejectCommandWorkflow.Execute(
            Actions(
                calls.Add,
                reject: () =>
                    new CodingInlineDefectRejectResult(
                        Rejected: false,
                        Event: null,
                        ShouldClearSelectedDefect: false)));

        Assert.Equal(["reject"], calls);
        Assert.Equal(CodingInlineDefectRejectCommandWorkflowOutcome.NotRejected, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_hides_refreshes_and_fades_without_clearing_selection()
    {
        var calls = new List<string>();

        var result = CodingInlineDefectRejectCommandWorkflow.Execute(
            Actions(
                calls.Add,
                reject: () => new CodingInlineDefectRejectResult(
                    Rejected: true,
                    Event: Event("BBA"),
                    ShouldClearSelectedDefect: false)));

        Assert.Equal(["reject", "hide-detail", "refresh", "fade"], calls);
        Assert.Equal(CodingInlineDefectRejectCommandWorkflowOutcome.Rejected, result.Outcome);
        Assert.True(result.Completed);
    }

    [Fact]
    public void Execute_clears_selection_before_hiding_refreshing_and_fading()
    {
        var calls = new List<string>();

        var result = CodingInlineDefectRejectCommandWorkflow.Execute(
            Actions(
                calls.Add,
                reject: () => new CodingInlineDefectRejectResult(
                    Rejected: true,
                    Event: Event("BBA"),
                    ShouldClearSelectedDefect: true)));

        Assert.Equal(["reject", "clear-selected", "hide-detail", "refresh", "fade"], calls);
        Assert.Equal(CodingInlineDefectRejectCommandWorkflowOutcome.Rejected, result.Outcome);
        Assert.True(result.Completed);
    }

    private static CodingInlineDefectRejectCommandActions Actions(
        Action<string> calls,
        Func<CodingInlineDefectRejectResult>? reject = null)
        => new(
            RejectDefect: () =>
            {
                calls("reject");
                return reject?.Invoke()
                    ?? new CodingInlineDefectRejectResult(
                        Rejected: true,
                        Event: Event("BBA"),
                        ShouldClearSelectedDefect: false);
            },
            ClearSelectedDefect: () => calls("clear-selected"),
            HideInlineDefectDetail: () => calls("hide-detail"),
            RefreshEvents: () => calls("refresh"),
            FadeOutAiOverlayAfterAction: () => calls("fade"));

    private static CodingEvent Event(string code)
        => new()
        {
            EventId = Guid.NewGuid(),
            Entry = new() { Code = code }
        };
}
