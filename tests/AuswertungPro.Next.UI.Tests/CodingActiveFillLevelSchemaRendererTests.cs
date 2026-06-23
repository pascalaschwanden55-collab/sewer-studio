using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingActiveFillLevelSchemaRendererTests
{
    [Fact]
    public void Render_adds_pipe_reference_fill_segment_level_line_marker_and_label()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var schema = new FillLevelSchema
            {
                PipeCenter = new NormalizedPoint(0.5, 0.5),
                PipeRadius = 0.35,
                FillRatio = 0.5,
                Mode = LevelMode.Water
            };
            var overlay = CodingSchemaOverlayBuilder.BuildGeometry(schema);

            var rendered = CodingActiveFillLevelSchemaRenderer.Render(
                canvas,
                schema,
                overlay,
                effect: null,
                toPixel: ToPixel,
                canvasWidth: 200,
                canvasHeight: 100);

            Assert.True(rendered);
            Assert.Equal(5, canvas.Children.Count);

            var pipe = Assert.IsType<Ellipse>(canvas.Children[0]);
            Assert.Equal(70, pipe.Width);
            Assert.Equal(70, pipe.Height);
            Assert.Equal(OverlayTags.Preview, pipe.Tag);

            var segment = Assert.IsType<Rectangle>(canvas.Children[1]);
            Assert.Equal(70, segment.Width);
            Assert.Equal(35, segment.Height);
            Assert.Equal(65, Canvas.GetLeft(segment));
            Assert.Equal(50, Canvas.GetTop(segment));
            Assert.Equal(OverlayTags.Preview, segment.Tag);

            var levelLine = Assert.IsType<Line>(canvas.Children[2]);
            Assert.Equal(30, levelLine.X1, 3);
            Assert.Equal(50, levelLine.Y1, 3);
            Assert.Equal(170, levelLine.X2, 3);
            Assert.Equal(50, levelLine.Y2, 3);
            Assert.Equal(6, levelLine.StrokeDashArray[0]);
            Assert.Equal(3, levelLine.StrokeDashArray[1]);

            Assert.IsType<Ellipse>(canvas.Children[3]);

            var label = Assert.IsType<TextBlock>(canvas.Children[4]);
            Assert.Equal("50.0%", label.Text);
            Assert.Equal(OverlayTags.Measure, label.Tag);
        });
    }

    [Fact]
    public void Render_returns_false_without_two_overlay_points()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();

            var rendered = CodingActiveFillLevelSchemaRenderer.Render(
                canvas,
                new FillLevelSchema(),
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
