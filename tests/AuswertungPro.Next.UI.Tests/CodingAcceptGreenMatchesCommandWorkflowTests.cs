using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAcceptGreenMatchesCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_when_coding_view_model_is_missing()
    {
        var result = await CodingAcceptGreenMatchesCommandWorkflow.ExecuteAsync(
            new CodingAcceptGreenMatchesCommandRequest(
                HasCodingViewModel: false,
                CurrentRouting: Routing()),
            Actions(
                runProtocolMatch: () => throw new InvalidOperationException("Match should not run."),
                acceptGreenMatchesAsync: _ => throw new InvalidOperationException("Accept should not run.")));

        Assert.Equal(CodingAcceptGreenMatchesCommandOutcome.NoCodingViewModel, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_runs_match_when_current_routing_is_missing_and_stops_when_still_missing()
    {
        var calls = new List<string>();

        var result = await CodingAcceptGreenMatchesCommandWorkflow.ExecuteAsync(
            new CodingAcceptGreenMatchesCommandRequest(
                HasCodingViewModel: true,
                CurrentRouting: null),
            Actions(
                runProtocolMatch: () => calls.Add("run"),
                getCurrentRouting: () =>
                {
                    calls.Add("get");
                    return null;
                },
                acceptGreenMatchesAsync: _ => throw new InvalidOperationException("Accept should not run.")));

        Assert.Equal(CodingAcceptGreenMatchesCommandOutcome.NoRouting, result.Outcome);
        Assert.False(result.Completed);
        Assert.Equal(["run", "get"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_accepts_green_matches_and_shows_overlay()
    {
        var routing = Routing();
        var overlay = new CodingProtocolMatchOverlayState("2 uebernommen", TimeSpan.FromSeconds(4));
        var calls = new List<string>();
        CodingProtocolMatchOverlayState? shown = null;

        var result = await CodingAcceptGreenMatchesCommandWorkflow.ExecuteAsync(
            new CodingAcceptGreenMatchesCommandRequest(
                HasCodingViewModel: true,
                CurrentRouting: routing),
            Actions(
                runProtocolMatch: () => calls.Add("run"),
                acceptGreenMatchesAsync: acceptedRouting =>
                {
                    Assert.Same(routing, acceptedRouting);
                    calls.Add("accept");
                    return Task.FromResult<CodingProtocolMatchOverlayState?>(overlay);
                },
                showOverlay: state =>
                {
                    shown = state;
                    calls.Add("overlay");
                }));

        Assert.Equal(CodingAcceptGreenMatchesCommandOutcome.OverlayShown, result.Outcome);
        Assert.True(result.Completed);
        Assert.Equal(["accept", "overlay"], calls);
        Assert.Equal(overlay, shown);
    }

    private static CodingAcceptGreenMatchesCommandActions Actions(
        Action? runProtocolMatch = null,
        Func<CodingMatchRouting?>? getCurrentRouting = null,
        Func<CodingMatchRouting, Task<CodingProtocolMatchOverlayState?>>? acceptGreenMatchesAsync = null,
        Action<CodingProtocolMatchOverlayState>? showOverlay = null)
        => new(
            RunProtocolMatch: runProtocolMatch ?? (() => { }),
            GetCurrentRouting: getCurrentRouting ?? (() => Routing()),
            AcceptGreenMatchesAsync: acceptGreenMatchesAsync ?? (_ => Task.FromResult<CodingProtocolMatchOverlayState?>(null)),
            ShowOverlay: showOverlay ?? (_ => { }));

    private static CodingMatchRouting Routing()
        => new(
            new BefundMatchResult(),
            Trainingskandidaten: [],
            ReviewGelb: [],
            FalscherCodeReview: [],
            Verpasst: [],
            Fehlalarm: []);
}
