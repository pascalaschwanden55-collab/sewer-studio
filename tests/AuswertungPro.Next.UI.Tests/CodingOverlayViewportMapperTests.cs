using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayViewportMapperTests
{
    [Fact]
    public void GetContentRect_uses_full_canvas_when_video_aspect_is_unknown()
    {
        var rect = CodingOverlayViewportMapper.GetContentRect(640, 480, 0);

        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(640, rect.Width);
        Assert.Equal(480, rect.Height);
    }

    [Fact]
    public void GetContentRect_letterboxes_wide_video_in_square_canvas()
    {
        var rect = CodingOverlayViewportMapper.GetContentRect(1000, 1000, 16.0 / 9.0);

        Assert.Equal(0, rect.X, precision: 6);
        Assert.Equal(218.75, rect.Y, precision: 6);
        Assert.Equal(1000, rect.Width, precision: 6);
        Assert.Equal(562.5, rect.Height, precision: 6);
    }

    [Fact]
    public void GetContentRect_pillarboxes_narrow_video_in_wide_canvas()
    {
        var rect = CodingOverlayViewportMapper.GetContentRect(1000, 500, 4.0 / 3.0);

        Assert.Equal(166.666666, rect.X, precision: 5);
        Assert.Equal(0, rect.Y, precision: 6);
        Assert.Equal(666.666666, rect.Width, precision: 5);
        Assert.Equal(500, rect.Height, precision: 6);
    }

    [Fact]
    public void PixelToNorm_maps_pixels_inside_visible_content_rect()
    {
        var content = new Rect(100, 50, 800, 400);

        var norm = CodingOverlayViewportMapper.PixelToNorm(new Point(500, 250), content);

        Assert.Equal(0.5, norm.X, precision: 6);
        Assert.Equal(0.5, norm.Y, precision: 6);
    }

    [Fact]
    public void PixelToNorm_returns_center_for_invalid_content_rect()
    {
        var norm = CodingOverlayViewportMapper.PixelToNorm(new Point(10, 20), new Rect(0, 0, 0, 0));

        Assert.Equal(0.5, norm.X);
        Assert.Equal(0.5, norm.Y);
    }

    [Fact]
    public void NormToPixel_maps_norm_inside_visible_content_rect()
    {
        var content = new Rect(100, 50, 800, 400);

        var point = CodingOverlayViewportMapper.NormToPixel(new NormalizedPoint(0.25, 0.75), content);

        Assert.Equal(300, point.X, precision: 6);
        Assert.Equal(350, point.Y, precision: 6);
    }
}
