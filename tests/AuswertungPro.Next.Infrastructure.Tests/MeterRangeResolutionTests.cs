using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Punkt- vs. Streckenschaden: ein Punktschaden (z.B. Anschluss), der ueber
/// mehrere Frames sichtbar ist, darf keine kuenstliche Strecke werden. Ein echter
/// Streckenschaden (z.B. Wurzeln) behaelt seinen MeterStart..MeterEnd-Bereich.
/// </summary>
public class MeterRangeResolutionTests
{
    [Fact]
    public void ResolveMeterEnd_PointDamage_CollapsesToStart()
    {
        // BCA = seitlicher Anschluss = Punktschaden -> eine Stelle
        var end = MultiModelAnalysisService.ResolveMeterEnd("BCA", 12.3, 12.9);
        Assert.Equal(12.3, end);
    }

    [Fact]
    public void ResolveMeterEnd_StretchDamage_KeepsObservedRange()
    {
        // BBA = Wurzeln = Streckenschaden -> Bereich behalten
        var end = MultiModelAnalysisService.ResolveMeterEnd("BBA", 12.3, 18.0);
        Assert.Equal(18.0, end);
    }

    [Fact]
    public void ResolveMeterEnd_UnknownOrNullCode_TreatedAsPoint()
    {
        Assert.Equal(12.3, MultiModelAnalysisService.ResolveMeterEnd(null, 12.3, 12.9));
        Assert.Equal(12.3, MultiModelAnalysisService.ResolveMeterEnd("", 12.3, 12.9));
    }
}
