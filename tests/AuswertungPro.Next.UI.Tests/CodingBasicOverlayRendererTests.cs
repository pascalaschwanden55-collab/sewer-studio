using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBasicOverlayRendererTests
{
    [Fact]
    public void Render_adds_preview_line_with_mapped_points()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style(isPreview: true);
            var overlay = Geometry(OverlayToolType.Line, (0.1, 0.2), (0.3, 0.4));

            var rendered = CodingBasicOverlayRenderer.Render(canvas, overlay, ToPixel, style);

            Assert.True(rendered);
            var line = Assert.IsType<Line>(Assert.Single(canvas.Children));
            Assert.Equal(10, line.X1);
            Assert.Equal(20, line.Y1);
            Assert.Equal(30, line.X2);
            Assert.Equal(40, line.Y2);
            Assert.Same(style.Stroke, line.Stroke);
            Assert.Equal(3, line.StrokeThickness);
            Assert.Equal(4, line.StrokeDashArray[0]);
            Assert.Equal(2, line.StrokeDashArray[1]);
            Assert.Equal(style.Tag, line.Tag);
        });
    }

    [Fact]
    public void Render_adds_rectangle_from_mapped_bounds()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style(isPreview: false);
            var overlay = Geometry(
                OverlayToolType.Rectangle,
                (0.4, 0.5),
                (0.1, 0.2),
                (0.4, 0.2),
                (0.1, 0.5));

            var rendered = CodingBasicOverlayRenderer.Render(canvas, overlay, ToPixel, style);

            Assert.True(rendered);
            var rect = Assert.IsType<Rectangle>(Assert.Single(canvas.Children));
            Assert.Equal(30, rect.Width);
            Assert.Equal(30, rect.Height);
            Assert.Equal(10, Canvas.GetLeft(rect));
            Assert.Equal(20, Canvas.GetTop(rect));
            Assert.Same(style.Stroke, rect.Stroke);
            Assert.Same(style.Fill, rect.Fill);
            Assert.Equal(style.Tag, rect.Tag);
        });
    }

    [Fact]
    public void Render_adds_centered_point_marker()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style(isPreview: false);
            var overlay = Geometry(OverlayToolType.Point, (0.5, 0.6));

            var rendered = CodingBasicOverlayRenderer.Render(canvas, overlay, ToPixel, style);

            Assert.True(rendered);
            var dot = Assert.IsType<Ellipse>(Assert.Single(canvas.Children));
            Assert.Equal(16, dot.Width);
            Assert.Equal(16, dot.Height);
            Assert.Equal(42, Canvas.GetLeft(dot));
            Assert.Equal(52, Canvas.GetTop(dot));
            Assert.Same(style.Stroke, dot.Fill);
            Assert.Same(Brushes.White, dot.Stroke);
            Assert.Equal(style.Tag, dot.Tag);
        });
    }

    [Fact]
    public void Render_adds_preview_ellipse_with_existing_style()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style(isPreview: true);
            var overlay = Geometry(OverlayToolType.Ellipse, (0.2, 0.2), (0.5, 0.7));

            var rendered = CodingBasicOverlayRenderer.Render(canvas, overlay, ToPixel, style);

            Assert.True(rendered);
            var ellipse = Assert.IsType<Ellipse>(Assert.Single(canvas.Children));
            Assert.Equal(30, ellipse.Width);
            Assert.Equal(50, ellipse.Height);
            Assert.Equal(20, Canvas.GetLeft(ellipse));
            Assert.Equal(20, Canvas.GetTop(ellipse));
            Assert.Same(Brushes.MediumPurple, ellipse.Stroke);
            Assert.Equal(2, ellipse.StrokeThickness);
            Assert.Equal(4, ellipse.StrokeDashArray[0]);
            Assert.Equal(2, ellipse.StrokeDashArray[1]);
            Assert.Equal(style.Tag, ellipse.Tag);
        });
    }

    [Fact]
    public void Render_adds_preview_freehand_polygon()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style(isPreview: true);
            var overlay = Geometry(OverlayToolType.Freehand, (0.1, 0.1), (0.2, 0.3), (0.4, 0.2));

            var rendered = CodingBasicOverlayRenderer.Render(canvas, overlay, ToPixel, style);

            Assert.True(rendered);
            var polygon = Assert.IsType<Polygon>(Assert.Single(canvas.Children));
            Assert.Equal(3, polygon.Points.Count);
            Assert.Equal(new Point(10, 10), polygon.Points[0]);
            Assert.Equal(new Point(20, 30), polygon.Points[1]);
            Assert.Equal(new Point(40, 20), polygon.Points[2]);
            Assert.Same(Brushes.HotPink, polygon.Stroke);
            Assert.Equal(PenLineJoin.Round, polygon.StrokeLineJoin);
            Assert.Equal(3, polygon.StrokeDashArray[0]);
            Assert.Equal(2, polygon.StrokeDashArray[1]);
            Assert.Equal(style.Tag, polygon.Tag);
        });
    }

    [Fact]
    public void Render_returns_false_when_geometry_is_incomplete()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style(isPreview: false);
            var overlay = Geometry(OverlayToolType.Rectangle, (0.1, 0.1), (0.2, 0.2));

            var rendered = CodingBasicOverlayRenderer.Render(canvas, overlay, ToPixel, style);

            Assert.False(rendered);
            Assert.Empty(canvas.Children);
        });
    }

    private static CodingBasicOverlayRenderStyle Style(bool isPreview)
        => new(
            isPreview,
            Brushes.Lime,
            Brushes.Cyan,
            Effect: null,
            Tag: isPreview ? OverlayTags.Preview : OverlayTags.Manual);

    private static OverlayGeometry Geometry(OverlayToolType tool, params (double X, double Y)[] points)
        => new()
        {
            ToolType = tool,
            Points = points.Select(p => new NormalizedPoint(p.X, p.Y)).ToList()
        };

    private static Point ToPixel(NormalizedPoint point)
        => new(point.X * 100, point.Y * 100);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
