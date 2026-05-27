using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class LiveDetectionMapperTests
{
    public LiveDetectionMapperTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    [Fact]
    public void FromEnhancedAnalysis_PreservesMeterAndBoundingBoxes()
    {
        var analysis = new EnhancedFrameAnalysis(
            Meter: 16.47,
            PipeMaterial: "beton",
            PipeDiameterMm: 300,
            Findings:
            [
                new EnhancedFinding(
                    Label: "Seitlicher Anschluss",
                    VsaCodeHint: "BCA",
                    Severity: 3,
                    PositionClock: "3",
                    ExtentPercent: 20,
                    HeightMm: 40,
                    WidthMm: 25,
                    IntrusionPercent: null,
                    CrossSectionReductionPercent: null,
                    DiameterReductionMm: null,
                    BboxX1: 0.10,
                    BboxY1: 0.20,
                    BboxX2: 0.30,
                    BboxY2: 0.40,
                    Notes: null)
            ],
            ImageQuality: "gut",
            IsEmptyFrame: false,
            Error: null);

        var result = LiveDetectionMapper.FromEnhancedAnalysis(analysis, 12.5);

        Assert.Equal(16.47, result.MeterReading);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("BCA", finding.VsaCodeHint);
        Assert.Equal(0.10, finding.BboxX1);
        Assert.Equal(0.20, finding.BboxY1);
        Assert.Equal(0.30, finding.BboxX2);
        Assert.Equal(0.40, finding.BboxY2);
    }

    [Fact]
    public void FromEnhancedAnalysis_RescuesResolvableFindingsOnBadImageQuality()
    {
        var analysis = new EnhancedFrameAnalysis(
            Meter: 5.25,
            PipeMaterial: "beton",
            PipeDiameterMm: 300,
            Findings:
            [
                new EnhancedFinding(
                    Label: "Riss",
                    VsaCodeHint: "BAB",
                    Severity: 4,
                    PositionClock: "12:00",
                    ExtentPercent: 10,
                    HeightMm: 5,
                    WidthMm: 2,
                    IntrusionPercent: null,
                    CrossSectionReductionPercent: null,
                    DiameterReductionMm: null,
                    BboxX1: 0.4,
                    BboxY1: 0.2,
                    BboxX2: 0.5,
                    BboxY2: 0.3,
                    Notes: null)
            ],
            ImageQuality: "schlecht",
            IsEmptyFrame: false,
            Error: null);

        var result = LiveDetectionMapper.FromEnhancedAnalysis(analysis, 3.0);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("BAB", finding.VsaCodeHint);
        Assert.Equal(3, finding.Severity); // quality downgrade 4 -> 3
        Assert.Equal(5.25, result.MeterReading);
        Assert.Null(result.Error);
    }

    [Fact]
    public void FromEnhancedAnalysis_BadImageQuality_DropsUnresolvableFindings()
    {
        var analysis = new EnhancedFrameAnalysis(
            Meter: 7.0,
            PipeMaterial: "beton",
            PipeDiameterMm: 300,
            Findings:
            [
                new EnhancedFinding(
                    Label: "normal pipe section",
                    VsaCodeHint: null,
                    Severity: 2,
                    PositionClock: null,
                    ExtentPercent: null,
                    HeightMm: null,
                    WidthMm: null,
                    IntrusionPercent: null,
                    CrossSectionReductionPercent: null,
                    DiameterReductionMm: null,
                    BboxX1: null,
                    BboxY1: null,
                    BboxX2: null,
                    BboxY2: null,
                    Notes: null)
            ],
            ImageQuality: "schlecht",
            IsEmptyFrame: false,
            Error: null);

        var result = LiveDetectionMapper.FromEnhancedAnalysis(analysis, 2.0);

        Assert.Empty(result.Findings);
        Assert.Equal(7.0, result.MeterReading);
    }

    [Theory]
    [InlineData("Wurzeleinwuchs", "BBA")]
    [InlineData("root intrusion", "BBA")]
    [InlineData("Inkrustation verkalkt", "BBB")]
    [InlineData("attached deposit", "BBB")]
    public void FromEnhancedAnalysis_BadImageQuality_UsesCentralVsaResolver(string label, string expectedCode)
    {
        var analysis = new EnhancedFrameAnalysis(
            Meter: 8.0,
            PipeMaterial: "beton",
            PipeDiameterMm: 300,
            Findings:
            [
                new EnhancedFinding(
                    Label: label,
                    VsaCodeHint: null,
                    Severity: 4,
                    PositionClock: null,
                    ExtentPercent: null,
                    HeightMm: null,
                    WidthMm: null,
                    IntrusionPercent: null,
                    CrossSectionReductionPercent: null,
                    DiameterReductionMm: null,
                    BboxX1: null,
                    BboxY1: null,
                    BboxX2: null,
                    BboxY2: null,
                    Notes: null)
            ],
            ImageQuality: "schlecht",
            IsEmptyFrame: false,
            Error: null);

        var result = LiveDetectionMapper.FromEnhancedAnalysis(analysis, 2.0);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(expectedCode, finding.VsaCodeHint);
    }
}
