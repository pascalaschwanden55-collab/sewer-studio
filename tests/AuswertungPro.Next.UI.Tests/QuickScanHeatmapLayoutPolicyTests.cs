using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class QuickScanHeatmapLayoutPolicyTests
{
    [Fact]
    public void CalculateSegmentLayout_maps_timestamp_to_track_position()
    {
        var layout = QuickScanHeatmapLayoutPolicy.CalculateSegmentLayout(
            timestampSeconds: 25,
            videoDurationSeconds: 100,
            trackOffsetX: 10,
            trackWidth: 400);

        Assert.Equal(110, layout.Left);
        Assert.Equal(20, layout.Width);
    }

    [Fact]
    public void CalculateSegmentLayout_clamps_timestamp_and_uses_minimum_width()
    {
        var beforeStart = QuickScanHeatmapLayoutPolicy.CalculateSegmentLayout(
            timestampSeconds: -5,
            videoDurationSeconds: 1000,
            trackOffsetX: 10,
            trackWidth: 100);
        var afterEnd = QuickScanHeatmapLayoutPolicy.CalculateSegmentLayout(
            timestampSeconds: 1100,
            videoDurationSeconds: 1000,
            trackOffsetX: 10,
            trackWidth: 100);

        Assert.Equal(10, beforeStart.Left);
        Assert.Equal(2, beforeStart.Width);
        Assert.Equal(110, afterEnd.Left);
        Assert.Equal(2, afterEnd.Width);
    }

    [Fact]
    public void CalculateSegmentLayout_returns_empty_width_for_invalid_bounds()
    {
        var layout = QuickScanHeatmapLayoutPolicy.CalculateSegmentLayout(
            timestampSeconds: 10,
            videoDurationSeconds: 0,
            trackOffsetX: 7,
            trackWidth: 200);

        Assert.Equal(7, layout.Left);
        Assert.Equal(0, layout.Width);
    }

    [Fact]
    public void EstimateDuration_uses_last_segment_plus_frame_step()
    {
        var segments = new[]
        {
            new QuickScanSegment(0, HasDamage: false, Severity: 0, Label: null, Clock: null),
            new QuickScanSegment(35, HasDamage: true, Severity: 3, Label: "Riss", Clock: "3")
        };

        var duration = QuickScanHeatmapLayoutPolicy.EstimateDuration(segments);

        Assert.Equal(40, duration);
    }
}
