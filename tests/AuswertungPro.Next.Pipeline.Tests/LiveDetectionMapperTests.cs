using System;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class LiveDetectionMapperTests
{
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
                    BboxX1Norm: 0.10,
                    BboxY1Norm: 0.20,
                    BboxX2Norm: 0.30,
                    BboxY2Norm: 0.40,
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
                    BboxX1Norm: 0.4,
                    BboxY1Norm: 0.2,
                    BboxX2Norm: 0.5,
                    BboxY2Norm: 0.3,
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
                    BboxX1Norm: null,
                    BboxY1Norm: null,
                    BboxX2Norm: null,
                    BboxY2Norm: null,
                    Notes: null)
            ],
            ImageQuality: "schlecht",
            IsEmptyFrame: false,
            Error: null);

        var result = LiveDetectionMapper.FromEnhancedAnalysis(analysis, 2.0);

        Assert.Empty(result.Findings);
        Assert.Equal(7.0, result.MeterReading);
    }
}
