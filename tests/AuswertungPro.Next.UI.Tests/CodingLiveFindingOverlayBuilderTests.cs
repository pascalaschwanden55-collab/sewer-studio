using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingOverlayBuilderTests
{
    [Fact]
    public void BuildRectangle_returns_null_when_bbox_is_incomplete()
    {
        var finding = Finding(x1: 0.1, y1: 0.2, x2: 0.3, y2: null);

        Assert.Null(CodingLiveFindingOverlayBuilder.BuildRectangle(finding));
    }

    [Fact]
    public void BuildRectangle_maps_complete_bbox_to_rectangle_overlay()
    {
        var overlay = CodingLiveFindingOverlayBuilder.BuildRectangle(
            Finding(x1: 0.1, y1: 0.2, x2: 0.3, y2: 0.4));

        Assert.NotNull(overlay);
        Assert.Equal(OverlayToolType.Rectangle, overlay.ToolType);
        AssertPoints(
            overlay,
            (0.1, 0.2),
            (0.3, 0.2),
            (0.3, 0.4),
            (0.1, 0.4));
    }

    [Fact]
    public void BuildRectangle_orders_inverted_bbox_corners()
    {
        var overlay = CodingLiveFindingOverlayBuilder.BuildRectangle(
            Finding(x1: 0.8, y1: 0.7, x2: 0.2, y2: 0.1));

        Assert.NotNull(overlay);
        AssertPoints(
            overlay,
            (0.2, 0.1),
            (0.8, 0.1),
            (0.8, 0.7),
            (0.2, 0.7));
    }

    private static void AssertPoints(OverlayGeometry overlay, params (double X, double Y)[] expected)
    {
        Assert.Equal(expected.Length, overlay.Points.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].X, overlay.Points[i].X, precision: 6);
            Assert.Equal(expected[i].Y, overlay.Points[i].Y, precision: 6);
        }
    }

    private static LiveFrameFinding Finding(
        double? x1,
        double? y1,
        double? x2,
        double? y2)
        => new(
            Label: "finding",
            Severity: 2,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: null,
            BboxX1: x1,
            BboxY1: y1,
            BboxX2: x2,
            BboxY2: y2);
}
