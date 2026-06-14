using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingDedupPolicyTests
{
    [Theory]
    [InlineData("BCD")]
    [InlineData("BCDA")]
    [InlineData("BCE")]
    [InlineData("BDC")]
    [InlineData("bdcxx")]
    public void IsOneTimeCode_RecognizesStartEndAndAbortCodes(string code)
    {
        Assert.True(CodingDedupPolicy.IsOneTimeCode(code));
    }

    [Theory]
    [InlineData("BCAEA", "BCA")]
    [InlineData("bcaea", "BCAAA")]
    [InlineData("BCD", "BCDA")]
    public void CodesMatch_UsesExactOrMainCode(string existingCode, string newCode)
    {
        Assert.True(CodingDedupPolicy.CodesMatch(existingCode, newCode));
    }

    [Fact]
    public void CodesMatch_RejectsDifferentMainCodes()
    {
        Assert.False(CodingDedupPolicy.CodesMatch("BCA", "BCC"));
    }

    [Fact]
    public void ShouldStopAnalysisAfterTerminalCode_StopsAtRohrendeMeter()
    {
        Assert.False(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BCE", 12.5, null)],
            currentMeter: 12.39,
            currentVideoTime: null));

        Assert.True(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BCE", 12.5, null)],
            currentMeter: 12.5,
            currentVideoTime: null));

        Assert.True(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BCE", 12.5, null)],
            currentMeter: 12.8,
            currentVideoTime: null));
    }

    [Fact]
    public void ShouldStopAnalysisAfterTerminalCode_StopsAfterAbortOrTimeWhenMeterMissing()
    {
        Assert.True(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BDC", null, TimeSpan.FromSeconds(80))],
            currentMeter: null,
            currentVideoTime: TimeSpan.FromSeconds(81)));

        Assert.False(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BCE", null, TimeSpan.FromSeconds(80))],
            currentMeter: null,
            currentVideoTime: TimeSpan.FromSeconds(79)));
    }

    [Fact]
    public void ShouldDeferSpatialCodeUntilCloser_DeferiertNurVorausBogen()
    {
        var previewBend = MetrierungProximityEvaluator.Evaluate(
            new MetrierungProximityInput(0.46, 0.46, 0.54, 0.54, 0.5, 0.5, 1.0, 0.5),
            MetrierungProximityThresholds.Default);

        Assert.False(previewBend.IsCodierbar);
        Assert.True(CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser("BCC", previewBend));
    }

    [Fact]
    public void ShouldDeferSpatialCodeUntilCloser_CodiertQuerschnittsfuellendenBogen()
    {
        var realBend = MetrierungProximityEvaluator.Evaluate(
            new MetrierungProximityInput(0.03, 0.03, 0.97, 0.97, 0.5, 0.5, 1.0, 0.5),
            MetrierungProximityThresholds.Default);

        Assert.True(realBend.IsCodierbar);
        Assert.False(CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser("BCC", realBend));
    }

    [Fact]
    public void ShouldDeferSpatialCodeUntilCloser_CodiertBogenWennNichtZentralVoraus()
    {
        var nearWallBend = MetrierungProximityEvaluator.Evaluate(
            new MetrierungProximityInput(0.46, 0.02, 0.54, 0.12, 0.5, 0.5, 1.0, 0.5),
            MetrierungProximityThresholds.Default);

        Assert.True(nearWallBend.IsCodierbar);
        Assert.False(CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser("BCC", nearWallBend));
        Assert.False(CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser("BAB", nearWallBend));
    }
}
