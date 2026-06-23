using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingArcOverlayRendererTests
{
    [Fact]
    public void Render_adds_clockwise_arc_path()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();

            var rendered = CodingArcOverlayRenderer.Render(
                canvas,
                start: new NormalizedPoint(0.5, 0.2),
                end: new NormalizedPoint(0.8, 0.5),
                center: new NormalizedPoint(0.5, 0.5),
                stroke: Brushes.Lime,
                effect: null,
                tag: OverlayTags.Preview,
                dashed: false,
                toPixel: ToPixel);

            Assert.True(rendered);
            var path = Assert.IsType<Path>(Assert.Single(canvas.Children));
            Assert.Same(Brushes.Lime, path.Stroke);
            Assert.Equal(3, path.StrokeThickness);
            Assert.Equal(OverlayTags.Preview, path.Tag);

            var geometry = Assert.IsType<PathGeometry>(path.Data);
            var figure = Assert.Single(geometry.Figures);
            Assert.Equal(new Point(100, 20), figure.StartPoint);
            var segment = Assert.IsType<ArcSegment>(Assert.Single(figure.Segments));
            Assert.Equal(new Point(130, 50), segment.Point);
            Assert.Equal(new Size(30, 30), segment.Size);
            Assert.False(segment.IsLargeArc);
            Assert.Equal(SweepDirection.Clockwise, segment.SweepDirection);
        });
    }

    [Fact]
    public void Render_adds_dash_array_for_preview_or_ai_arc()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();

            var rendered = CodingArcOverlayRenderer.Render(
                canvas,
                start: new NormalizedPoint(0.5, 0.2),
                end: new NormalizedPoint(0.8, 0.5),
                center: new NormalizedPoint(0.5, 0.5),
                stroke: Brushes.Orange,
                effect: null,
                tag: OverlayTags.AiOverlay,
                dashed: true,
                toPixel: ToPixel);

            Assert.True(rendered);
            var path = Assert.IsType<Path>(Assert.Single(canvas.Children));
            Assert.Equal(4, path.StrokeDashArray[0]);
            Assert.Equal(2, path.StrokeDashArray[1]);
        });
    }

    [Fact]
    public void Render_returns_false_when_arc_radius_is_too_small()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();

            var rendered = CodingArcOverlayRenderer.Render(
                canvas,
                start: new NormalizedPoint(0.5, 0.5),
                end: new NormalizedPoint(0.8, 0.5),
                center: new NormalizedPoint(0.5, 0.5),
                stroke: Brushes.Lime,
                effect: null,
                tag: OverlayTags.Preview,
                dashed: false,
                toPixel: ToPixel);

            Assert.False(rendered);
            Assert.Empty(canvas.Children);
        });
    }

    private static Point ToPixel(NormalizedPoint point)
        => new(point.X * 200, point.Y * 100);

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
