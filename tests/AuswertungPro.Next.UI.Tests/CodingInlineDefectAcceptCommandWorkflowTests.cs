using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingInlineDefectAcceptCommandWorkflowTests
{
    [Fact]
    public void Execute_runs_accept_and_stops_without_accepted_event()
    {
        var calls = new List<string>();

        var result = CodingInlineDefectAcceptCommandWorkflow.Execute(
            Actions(
                calls.Add,
                accept: () =>
                {
                    calls.Add("accept");
                    return null;
                }));

        Assert.Equal(["accept"], calls);
        Assert.Equal(CodingInlineDefectAcceptCommandWorkflowOutcome.NotAccepted, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_updates_detail_refreshes_and_fades_after_accept()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingInlineDefectAcceptCommandWorkflow.Execute(
            Actions(
                calls.Add,
                accept: () =>
                {
                    calls.Add("accept");
                    return ev;
                }));

        Assert.Equal(["accept", "detail:BBA", "refresh", "fade"], calls);
        Assert.Equal(CodingInlineDefectAcceptCommandWorkflowOutcome.Accepted, result.Outcome);
        Assert.True(result.Completed);
    }

    private static CodingInlineDefectAcceptCommandActions Actions(
        Action<string> calls,
        Func<CodingEvent?>? accept = null)
        => new(
            AcceptDefect: accept ?? (() => Event("BBA")),
            UpdateInlineDefectDetail: ev => calls($"detail:{ev.Entry.Code}"),
            RefreshEvents: () => calls("refresh"),
            FadeOutAiOverlayAfterAction: () => calls("fade"));

    private static CodingEvent Event(string code)
        => new()
        {
            EventId = Guid.NewGuid(),
            Entry = new() { Code = code }
        };
}
