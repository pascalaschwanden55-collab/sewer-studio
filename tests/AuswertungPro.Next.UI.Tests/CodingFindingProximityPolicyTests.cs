using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingFindingProximityPolicyTests
{
    [Fact]
    public void IsTooFarAhead_does_not_block_findings_without_bbox()
    {
        Assert.False(CodingFindingProximityPolicy.IsTooFarAhead(
            Finding(null, null, null, null),
            calibration: null,
            videoAspect: 1.0));
    }

    [Fact]
    public void IsTooFarAhead_blocks_small_central_bbox_inside_pipe_circle()
    {
        Assert.True(CodingFindingProximityPolicy.IsTooFarAhead(
            Finding(0.46, 0.46, 0.54, 0.54),
            calibration: null,
            videoAspect: 1.0));
    }

    [Fact]
    public void IsTooFarAhead_allows_wall_near_bbox()
    {
        Assert.False(CodingFindingProximityPolicy.IsTooFarAhead(
            Finding(0.46, 0.02, 0.54, 0.12),
            calibration: null,
            videoAspect: 1.0));
    }

    [Fact]
    public void IsTooFarAhead_uses_calibration_center_and_radius()
    {
        var calibration = new PipeCalibration
        {
            PipeCenter = new NormalizedPoint(0.7, 0.2),
            NormalizedDiameter = 0.5
        };

        Assert.True(CodingFindingProximityPolicy.IsTooFarAhead(
            Finding(0.68, 0.18, 0.72, 0.22),
            calibration,
            videoAspect: 1.0));
    }

    private static LiveFrameFinding Finding(double? x1, double? y1, double? x2, double? y2)
        => new(
            Label: "crack",
            Severity: 2,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: "BAB",
            BboxX1: x1,
            BboxY1: y1,
            BboxX2: x2,
            BboxY2: y2);
}
