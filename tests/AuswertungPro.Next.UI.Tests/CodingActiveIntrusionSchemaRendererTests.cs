using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingActiveIntrusionSchemaRendererTests
{
    [Fact]
    public void Render_adds_pipe_reference_intrusion_shape_handles_and_label()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var schema = new IntrusionSchema
            {
                PipeCenter = new NormalizedPoint(0.5, 0.5),
                PipeRadius = 0.35,
                ClockHour = 9,
                DepthRatio = 0.15,
                SpreadDeg = 30
            };
            var overlay = CodingSchemaOverlayBuilder.BuildGeometry(schema);

            var rendered = CodingActiveIntrusionSchemaRenderer.Render(
                canvas,
                schema,
                overlay,
                effect: null,
                toPixel: ToPixel,
                canvasWidth: 200,
                canvasHeight: 100);

            Assert.True(rendered);
            Assert.Equal(6, canvas.Children.Count);

            var pipe = Assert.IsType<Ellipse>(canvas.Children[0]);
            Assert.Equal(70, pipe.Width);
            Assert.Equal(70, pipe.Height);
            Assert.Equal(OverlayTags.Preview, pipe.Tag);

            var tongue = Assert.IsType<Polygon>(canvas.Children[1]);
            Assert.Equal(3, tongue.Points.Count);
            Assert.Equal(OverlayTags.Preview, tongue.Tag);

            var spine = Assert.IsType<Line>(canvas.Children[2]);
            Assert.Equal(30, spine.X1, 3);
            Assert.Equal(50, spine.Y1, 3);
            Assert.Equal(40.5, spine.X2, 3);
            Assert.Equal(50, spine.Y2, 3);
            Assert.Equal(4, spine.StrokeDashArray[0]);
            Assert.Equal(2, spine.StrokeDashArray[1]);

            Assert.IsType<Ellipse>(canvas.Children[3]);
            Assert.IsType<Ellipse>(canvas.Children[4]);

            var label = Assert.IsType<TextBlock>(canvas.Children[5]);
            Assert.Equal("15.0% @ 9.0h", label.Text);
            Assert.Equal(OverlayTags.Measure, label.Tag);
        });
    }

    [Fact]
    public void Render_returns_false_without_overlay()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();

            var rendered = CodingActiveIntrusionSchemaRenderer.Render(
                canvas,
                new IntrusionSchema(),
                overlay: null,
                effect: null,
                toPixel: ToPixel,
                canvasWidth: 200,
                canvasHeight: 100);

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
