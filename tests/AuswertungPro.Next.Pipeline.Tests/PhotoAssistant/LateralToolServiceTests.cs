using System;
using AuswertungPro.Next.Application.Ai.PhotoAssistant;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests.PhotoAssistant;

[Trait("Category", "PhotoAssistant")]
[Trait("Category", "Unit")]
public class LateralToolServiceTests
{
    [Fact]
    public void SichelBeiLateral90Grad_IstSymmetrisch()
    {
        var math = LateralToolService.ComputeMath(
            baseRadius: 100, lateralRelative: 0.5, lateralAngleDegrees: 90);

        Assert.Equal(0, math.AngleSkew, precision: 6);
        Assert.True(math.InnerSagitta > 0, "innerSagitta sollte bei 90° positiv sein");
        // innerSagitta = sh * 0.55 - skewOffset*0.3, bei skewOffset=0 -> innerSagitta = 0.55 * sh
        var expectedRatio = 0.55;
        var ratio = math.InnerSagitta / math.OuterSagitta;
        Assert.InRange(ratio, expectedRatio - 0.05, expectedRatio + 0.05);
    }

    [Fact]
    public void SichelBeiLateral30Grad_IstAsymmetrisch()
    {
        var math = LateralToolService.ComputeMath(
            baseRadius: 100, lateralRelative: 0.5, lateralAngleDegrees: 30);

        // angleSkew = (30-90)/60 = -1
        Assert.Equal(-1, math.AngleSkew, precision: 6);
        Assert.True(Math.Abs(math.SkewOffset) > 0.001, $"skewOffset sollte signifikant != 0 sein, war {math.SkewOffset}");
    }

    [Fact]
    public void SichelPath_IstNichtLeer()
    {
        var path = LateralToolService.BuildSichelPathData(
            baseRadius: 100, lateralRelative: 0.5, lateralAngleDegrees: 90);
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.StartsWith("M", path);
        Assert.Contains(" Z", path);
    }
}
