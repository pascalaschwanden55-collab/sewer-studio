using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchSummaryFormatterTests
{
    [Fact]
    public void Format_returns_not_run_text_for_null_routing()
    {
        Assert.Equal("Abgleich: noch nicht ausgefuehrt", CodingProtocolMatchSummaryFormatter.Format(null));
        Assert.False(CodingProtocolMatchSummaryFormatter.CanAcceptGreenMatches(null));
    }

    [Fact]
    public void Format_includes_bucket_counts_precision_and_recall()
    {
        var result = new BefundMatchResult();
        result.Treffer.Add(Pair("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002"));
        result.Treffer.Add(Pair("00000000-0000-0000-0000-000000000003", "00000000-0000-0000-0000-000000000004"));
        result.FalscherCode.Add(Pair("00000000-0000-0000-0000-000000000005", "00000000-0000-0000-0000-000000000006"));
        result.Verpasst.Add(Finding("00000000-0000-0000-0000-000000000007"));
        result.Fehlalarm.Add(Finding("00000000-0000-0000-0000-000000000008"));
        var routing = new CodingMatchRouting(
            result,
            Trainingskandidaten: [result.Treffer[0]],
            ReviewGelb: [result.Treffer[1]],
            FalscherCodeReview: result.FalscherCode,
            Verpasst: result.Verpasst,
            Fehlalarm: result.Fehlalarm);

        var summary = CodingProtocolMatchSummaryFormatter.Format(routing);

        Assert.Equal("Abgleich: 2 Treffer (1 gruen/1 gelb) | 1 falscher Code | 1 fehlen | 1 extra | P 50% R 50%",
            summary);
        Assert.True(CodingProtocolMatchSummaryFormatter.CanAcceptGreenMatches(routing));
    }

    private static BefundMatchPair Pair(string gtRefId, string kiRefId)
        => new(Finding(gtRefId), Finding(kiRefId), 0.0, "gruen");

    private static BefundMatchFinding Finding(string refId)
        => new("BAB", 1.0, 1.0, "Riss", refId);
}
