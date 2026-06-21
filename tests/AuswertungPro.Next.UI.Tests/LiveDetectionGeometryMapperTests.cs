using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionGeometryMapperTests
{
    [Theory]
    [InlineData("3 Uhr", 3)]
    [InlineData("09", 9)]
    [InlineData("12-2", 12)]
    [InlineData("", null)]
    [InlineData("unknown", null)]
    public void ParseClockHour_extracts_supported_clock_hour(string raw, int? expected)
    {
        Assert.Equal(expected, LiveDetectionGeometryMapper.ParseClockHour(raw));
    }

    [Fact]
    public void BuildRingSectorGeometry_returns_closed_four_segment_path()
    {
        var geometry = Assert.IsType<PathGeometry>(
            LiveDetectionGeometryMapper.BuildRingSectorGeometry(100, 100, 20, 40, -90, 60));

        var figure = Assert.Single(geometry.Figures);
        Assert.True(figure.IsClosed);
        Assert.Equal(3, figure.Segments.Count);
    }

    [Fact]
    public void EstimateClockFromOverlayCenter_maps_points_relative_to_pipe_center()
    {
        var top = new OverlayGeometry { Points = { new NormalizedPoint(0.5, 0.1) } };
        var right = new OverlayGeometry { Points = { new NormalizedPoint(0.9, 0.5) } };
        var empty = new OverlayGeometry();

        Assert.Equal("12", LiveDetectionGeometryMapper.EstimateClockFromOverlayCenter(top));
        Assert.Equal("3", LiveDetectionGeometryMapper.EstimateClockFromOverlayCenter(right));
        Assert.Null(LiveDetectionGeometryMapper.EstimateClockFromOverlayCenter(empty));
    }

    [Fact]
    public void BoxContainsVanishingPoint_applies_small_tolerance_around_overlay_box()
    {
        var overlay = new OverlayGeometry
        {
            Points =
            {
                new NormalizedPoint(0.3, 0.4),
                new NormalizedPoint(0.5, 0.6)
            }
        };

        Assert.True(LiveDetectionGeometryMapper.BoxContainsVanishingPoint(overlay, 0.52, 0.39));
        Assert.False(LiveDetectionGeometryMapper.BoxContainsVanishingPoint(overlay, 0.7, 0.5));
        Assert.False(LiveDetectionGeometryMapper.BoxContainsVanishingPoint(null, 0.4, 0.5));
    }

    [Fact]
    public void BBoxFromClockPosition_maps_clock_and_extent_to_normalized_box()
    {
        var finding = new LiveFrameFinding("Riss", 3, "12", 25);

        var bbox = LiveDetectionGeometryMapper.BBoxFromClockPosition(finding);

        Assert.Equal(0.5, bbox.XCenter, precision: 3);
        Assert.Equal(0.15, bbox.YCenter, precision: 3);
        Assert.Equal(0.15, bbox.Width, precision: 3);
        Assert.Equal(0.15, bbox.Height, precision: 3);
    }

    [Fact]
    public void ClickToClockPosition_maps_canvas_quadrants_to_clock_hours()
    {
        var size = new Size(200, 200);

        Assert.Equal("12", LiveDetectionGeometryMapper.ClickToClockPosition(new Point(100, 0), size));
        Assert.Equal("3", LiveDetectionGeometryMapper.ClickToClockPosition(new Point(200, 100), size));
        Assert.Equal("6", LiveDetectionGeometryMapper.ClickToClockPosition(new Point(100, 200), size));
        Assert.Equal("9", LiveDetectionGeometryMapper.ClickToClockPosition(new Point(0, 100), size));
    }
}
