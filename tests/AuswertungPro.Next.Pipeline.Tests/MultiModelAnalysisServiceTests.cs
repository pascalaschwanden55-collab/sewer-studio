using System;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;
using AuswertungPro.Next.Application.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

[Trait("Category", "Unit")]
public sealed class MultiModelAnalysisServiceTests
{
    [Fact]
    public void ToEnhancedAnalysis_PreservesNormalizedBboxAndInferredCode()
    {
        var result = new MultiModelFrameResult(
            TimestampSec: 12.0,
            Meter: 16.4,
            IsRelevant: true,
            DinoDetections: Array.Empty<DinoDetectionDto>(),
            SamMasks:
            [
                new SamMaskResult(
                    Label: "Seitlicher Anschluss",
                    Confidence: 0.91,
                    Bbox: [420, 160, 580, 320],
                    MaskRle: "",
                    MaskAreaPixels: 5000,
                    ImageAreaPixels: 640 * 480,
                    HeightPixels: 160,
                    WidthPixels: 160,
                    CentroidX: 500,
                    CentroidY: 240)
            ],
            ImageWidth: 640,
            ImageHeight: 480,
            YoloTimeMs: 5,
            DinoTimeMs: 7,
            SamTimeMs: 9);

        var analysis = MultiModelAnalysisService.ToEnhancedAnalysis(result, 300);

        var finding = Assert.Single(analysis.Findings);
        Assert.Equal("BCA", finding.VsaCodeHint);
        Assert.Equal("3:00", finding.PositionClock);
        Assert.Equal(420.0 / 640.0, finding.BboxX1Norm);
        Assert.Equal(160.0 / 480.0, finding.BboxY1Norm);
        Assert.Equal(580.0 / 640.0, finding.BboxX2Norm);
        Assert.Equal(320.0 / 480.0, finding.BboxY2Norm);
    }
}
