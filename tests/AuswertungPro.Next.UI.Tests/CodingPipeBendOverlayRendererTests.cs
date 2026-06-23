using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPipeBendOverlayRendererTests
{
    [Fact]
    public void Render_adds_two_point_preview_line_and_markers()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.PipeBend,
                Points =
                [
                    new NormalizedPoint(0.2, 0.4),
                    new NormalizedPoint(0.6, 0.4)
                ]
            };

            var rendered = CodingPipeBendOverlayRenderer.Render(
                canvas,
                overlay,
                isPreview: true,
                effect: null,
                tag: OverlayTags.Preview,
                labelTag: OverlayTags.Measure,
                toPixel: ToPixel);

            Assert.True(rendered);
            Assert.Equal(3, canvas.Children.Count);
            var line = Assert.IsType<Line>(canvas.Children[0]);
            Assert.Equal(40, line.X1);
            Assert.Equal(40, line.Y1);
            Assert.Equal(120, line.X2);
            Assert.Equal(40, line.Y2);
            Assert.Equal(2.5, line.StrokeThickness);
            Assert.Equal(4, line.StrokeDashArray[0]);
            Assert.Equal(2, line.StrokeDashArray[1]);
            Assert.IsType<Ellipse>(canvas.Children[1]);
            Assert.IsType<Ellipse>(canvas.Children[2]);
        });
    }

    [Fact]
    public void Render_adds_bend_lines_arc_markers_and_angle_label()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.PipeBend,
                Points =
                [
                    new NormalizedPoint(0.2, 0.5),
                    new NormalizedPoint(0.5, 0.5),
                    new NormalizedPoint(0.5, 0.2)
                ],
                ArcDegrees = 90
            };

            var rendered = CodingPipeBendOverlayRenderer.Render(
                canvas,
                overlay,
                isPreview: false,
                effect: null,
                tag: OverlayTags.Manual,
                labelTag: OverlayTags.Manual,
                toPixel: ToPixel);

            Assert.True(rendered);
            Assert.Equal(7, canvas.Children.Count);
            Assert.IsType<Line>(canvas.Children[0]);
            Assert.IsType<Line>(canvas.Children[1]);
            Assert.IsType<Ellipse>(canvas.Children[2]);
            Assert.IsType<Ellipse>(canvas.Children[3]);
            Assert.IsType<Ellipse>(canvas.Children[4]);
            Assert.IsType<Path>(canvas.Children[5]);

            var label = Assert.IsType<TextBlock>(canvas.Children[6]);
            Assert.Equal("90.0°", label.Text);
            Assert.Equal(OverlayTags.Manual, label.Tag);
            Assert.Equal(114, Canvas.GetLeft(label));
            Assert.Equal(26, Canvas.GetTop(label));
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
