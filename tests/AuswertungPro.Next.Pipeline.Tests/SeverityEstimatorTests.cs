using AuswertungPro.Next.Application.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public class SeverityEstimatorTests
{
    private static MaskQuantificationService.QuantifiedMask Mask(
        int? heightMm = null,
        int? extentPercent = null,
        int? crossSectionReductionPercent = null)
        => new(
            Label: "test",
            Confidence: 0.8,
            HeightMm: heightMm,
            WidthMm: null,
            ExtentPercent: extentPercent,
            CrossSectionReductionPercent: crossSectionReductionPercent,
            IntrusionPercent: null,
            ClockPosition: null);

    [Fact]
    public void CrossSectionReductionAbove50_ReturnsFive()
        => Assert.Equal(5, SeverityEstimator.Estimate(Mask(crossSectionReductionPercent: 60)));

    [Fact]
    public void CrossSectionReductionAbove25_ReturnsFour()
        => Assert.Equal(4, SeverityEstimator.Estimate(Mask(crossSectionReductionPercent: 30)));

    [Fact]
    public void ExtentAbove50_ReturnsFour()
        => Assert.Equal(4, SeverityEstimator.Estimate(Mask(extentPercent: 60)));

    [Fact]
    public void HeightAbove50_ReturnsThree()
        => Assert.Equal(3, SeverityEstimator.Estimate(Mask(heightMm: 60)));

    [Fact]
    public void ExtentAbove25_ReturnsThree()
        => Assert.Equal(3, SeverityEstimator.Estimate(Mask(extentPercent: 30)));

    [Fact]
    public void HeightAbove10_ReturnsTwo()
        => Assert.Equal(2, SeverityEstimator.Estimate(Mask(heightMm: 15)));

    [Fact]
    public void NothingSet_ReturnsOne()
        => Assert.Equal(1, SeverityEstimator.Estimate(Mask()));

    [Fact]
    public void BoundaryValues_NotInclusive()
    {
        // 50 ist NICHT > 50 → faellt durch CrossSection und Extent durch
        // 10 ist NICHT > 10 → faellt durch Height durch
        // → erwartet 1
        Assert.Equal(1, SeverityEstimator.Estimate(
            Mask(heightMm: 10, extentPercent: 25, crossSectionReductionPercent: 25)));
    }

    [Fact]
    public void StrengstesKriterium_GewinntZuerst()
    {
        // Sowohl CrossSection 60 (→5) als auch Height 5 (→1) gesetzt: 5 gewinnt
        Assert.Equal(5, SeverityEstimator.Estimate(
            Mask(heightMm: 5, crossSectionReductionPercent: 60)));
    }
}
