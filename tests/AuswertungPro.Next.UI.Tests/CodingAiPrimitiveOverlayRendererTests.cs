using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiPrimitiveOverlayRendererTests
{
    [Fact]
    public void Render_adds_dashed_ai_line_with_mapped_points()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style();
            var overlay = Geometry(OverlayToolType.Line, (0.1, 0.2), (0.3, 0.4));

            var rendered = CodingAiPrimitiveOverlayRenderer.Render(canvas, overlay, 200, 100, style);

            Assert.True(rendered);
            var line = Assert.IsType<Line>(Assert.Single(canvas.Children));
            Assert.Equal(20, line.X1);
            Assert.Equal(20, line.Y1);
            Assert.Equal(60, line.X2);
            Assert.Equal(40, line.Y2);
            Assert.Same(style.Stroke, line.Stroke);
            Assert.Equal(2.5, line.StrokeThickness);
            Assert.Equal(5, line.StrokeDashArray[0]);
            Assert.Equal(3, line.StrokeDashArray[1]);
            Assert.Equal(style.Tag, line.Tag);
        });
    }

    [Fact]
    public void Render_adds_centered_ai_point_marker()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style();
            var overlay = Geometry(OverlayToolType.Point, (0.5, 0.6));

            var rendered = CodingAiPrimitiveOverlayRenderer.Render(canvas, overlay, 200, 100, style);

            Assert.True(rendered);
            var dot = Assert.IsType<Ellipse>(Assert.Single(canvas.Children));
            Assert.Equal(14, dot.Width);
            Assert.Equal(14, dot.Height);
            Assert.Equal(93, Canvas.GetLeft(dot));
            Assert.Equal(53, Canvas.GetTop(dot));
            Assert.Same(style.Stroke, dot.Fill);
            Assert.Equal(0.8, dot.Opacity);
            Assert.Same(Brushes.White, dot.Stroke);
            Assert.Equal(1.5, dot.StrokeThickness);
            Assert.Equal(style.Tag, dot.Tag);
        });
    }

    [Fact]
    public void Render_returns_false_for_incomplete_or_unsupported_geometry()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style();

            Assert.False(CodingAiPrimitiveOverlayRenderer.Render(
                canvas,
                Geometry(OverlayToolType.Line, (0.1, 0.2)),
                200,
                100,
                style));
            Assert.False(CodingAiPrimitiveOverlayRenderer.Render(
                canvas,
                Geometry(OverlayToolType.Rectangle, (0.1, 0.2), (0.3, 0.4)),
                200,
                100,
                style));
            Assert.Empty(canvas.Children);
        });
    }

    private static CodingAiPrimitiveOverlayRenderStyle Style()
        => new(Brushes.Orange, Effect: null, OverlayTags.AiOverlay);

    private static OverlayGeometry Geometry(OverlayToolType tool, params (double X, double Y)[] points)
        => new()
        {
            ToolType = tool,
            Points = points.Select(p => new NormalizedPoint(p.X, p.Y)).ToList()
        };

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
