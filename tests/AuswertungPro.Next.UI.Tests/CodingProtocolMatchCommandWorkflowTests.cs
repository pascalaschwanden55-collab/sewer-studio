using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_when_coding_view_model_is_missing()
    {
        var result = CodingProtocolMatchCommandWorkflow.Execute(
            new CodingProtocolMatchCommandRequest(HasCodingViewModel: false),
            Actions(
                runMatch: () => throw new InvalidOperationException("Match should not run.")));

        Assert.Equal(CodingProtocolMatchCommandOutcome.NoCodingViewModel, result.Outcome);
        Assert.False(result.Completed);
        Assert.Null(result.Routing);
    }

    [Fact]
    public void Execute_runs_match_and_updates_ui_in_order()
    {
        var routing = Routing();
        var calls = new List<string>();
        CodingMatchRouting? stored = null;
        CodingMatchRouting? summarized = null;

        var result = CodingProtocolMatchCommandWorkflow.Execute(
            new CodingProtocolMatchCommandRequest(HasCodingViewModel: true),
            Actions(
                runMatch: () =>
                {
                    calls.Add("run");
                    return routing;
                },
                storeMatch: storedRouting =>
                {
                    stored = storedRouting;
                    calls.Add("store");
                },
                updateSummary: summaryRouting =>
                {
                    summarized = summaryRouting;
                    calls.Add("summary");
                },
                refreshEvents: () => calls.Add("refresh"),
                scheduleHighlights: () => calls.Add("highlights")));

        Assert.Equal(CodingProtocolMatchCommandOutcome.Completed, result.Outcome);
        Assert.True(result.Completed);
        Assert.Same(routing, result.Routing);
        Assert.Same(routing, stored);
        Assert.Same(routing, summarized);
        Assert.Equal(["run", "store", "summary", "refresh", "highlights"], calls);
    }

    private static CodingProtocolMatchCommandActions Actions(
        Func<CodingMatchRouting>? runMatch = null,
        Action<CodingMatchRouting>? storeMatch = null,
        Action<CodingMatchRouting>? updateSummary = null,
        Action? refreshEvents = null,
        Action? scheduleHighlights = null)
        => new(
            RunMatch: runMatch ?? (() => Routing()),
            StoreMatch: storeMatch ?? (_ => { }),
            UpdateSummary: updateSummary ?? (_ => { }),
            RefreshEvents: refreshEvents ?? (() => { }),
            ScheduleHighlights: scheduleHighlights ?? (() => { }));

    private static CodingMatchRouting Routing()
        => new(
            new BefundMatchResult(),
            Trainingskandidaten: [],
            ReviewGelb: [],
            FalscherCodeReview: [],
            Verpasst: [],
            Fehlalarm: []);
}
