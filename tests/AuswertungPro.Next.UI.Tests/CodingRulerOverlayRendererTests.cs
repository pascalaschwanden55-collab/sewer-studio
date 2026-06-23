using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingRulerOverlayRendererTests
{
    [Fact]
    public void Render_adds_ruler_line_ticks_and_total_label()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.Ruler,
                Points =
                [
                    new NormalizedPoint(0.1, 0.5),
                    new NormalizedPoint(0.6, 0.5)
                ],
                Q1Mm = 100
            };

            var rendered = CodingRulerOverlayRenderer.Render(
                canvas,
                overlay,
                isPreview: true,
                effect: null,
                tag: OverlayTags.Preview,
                labelTag: OverlayTags.Measure,
                toPixel: ToPixel,
                labelAnchor: null);

            Assert.True(rendered);
            Assert.Equal(17, canvas.Children.Count);

            var mainLine = Assert.IsType<Line>(canvas.Children[0]);
            Assert.Equal(20, mainLine.X1);
            Assert.Equal(50, mainLine.Y1);
            Assert.Equal(120, mainLine.X2);
            Assert.Equal(50, mainLine.Y2);
            Assert.Equal(2.5, mainLine.StrokeThickness);
            Assert.Equal(4, mainLine.StrokeDashArray[0]);
            Assert.Equal(2, mainLine.StrokeDashArray[1]);
            Assert.Equal(OverlayTags.Preview, mainLine.Tag);

            var totalLabel = Assert.IsType<TextBlock>(canvas.Children[^1]);
            Assert.Equal("100.0 mm", totalLabel.Text);
            Assert.Equal(OverlayTags.Measure, totalLabel.Tag);
            Assert.Equal(82, Canvas.GetLeft(totalLabel));
            Assert.Equal(30, Canvas.GetTop(totalLabel));
        });
    }

    [Fact]
    public void Render_returns_false_without_enough_points_or_positive_length()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();

            Assert.False(CodingRulerOverlayRenderer.Render(
                canvas,
                new OverlayGeometry
                {
                    ToolType = OverlayToolType.Ruler,
                    Points = [new NormalizedPoint(0.1, 0.5)],
                    Q1Mm = 100
                },
                isPreview: false,
                effect: null,
                tag: OverlayTags.Manual,
                labelTag: OverlayTags.Manual,
                toPixel: ToPixel,
                labelAnchor: null));

            Assert.False(CodingRulerOverlayRenderer.Render(
                canvas,
                new OverlayGeometry
                {
                    ToolType = OverlayToolType.Ruler,
                    Points =
                    [
                        new NormalizedPoint(0.1, 0.5),
                        new NormalizedPoint(0.6, 0.5)
                    ],
                    Q1Mm = 0
                },
                isPreview: false,
                effect: null,
                tag: OverlayTags.Manual,
                labelTag: OverlayTags.Manual,
                toPixel: ToPixel,
                labelAnchor: null));

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
