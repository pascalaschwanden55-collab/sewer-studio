using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLateralCircleOverlayRendererTests
{
    [Fact]
    public void Render_adds_circle_center_dot_radius_line_and_label()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.LateralCircle,
                Points =
                [
                    new NormalizedPoint(0.5, 0.5),
                    new NormalizedPoint(0.6, 0.5)
                ],
                Q1Mm = 150,
                DnRatioPercent = 50
            };

            var rendered = CodingLateralCircleOverlayRenderer.Render(
                canvas,
                overlay,
                isPreview: true,
                effect: null,
                tag: OverlayTags.Preview,
                labelTag: OverlayTags.Measure,
                toPixel: ToPixel);

            Assert.True(rendered);
            Assert.Equal(4, canvas.Children.Count);

            var circle = Assert.IsType<Ellipse>(canvas.Children[0]);
            Assert.Equal(40, circle.Width, precision: 6);
            Assert.Equal(40, circle.Height, precision: 6);
            Assert.Equal(80, Canvas.GetLeft(circle), precision: 6);
            Assert.Equal(30, Canvas.GetTop(circle), precision: 6);
            Assert.Equal(2.5, circle.StrokeThickness);
            Assert.Equal(4, circle.StrokeDashArray[0]);
            Assert.Equal(2, circle.StrokeDashArray[1]);
            Assert.Equal(OverlayTags.Preview, circle.Tag);

            Assert.IsType<Ellipse>(canvas.Children[1]);
            Assert.IsType<Line>(canvas.Children[2]);
            var label = Assert.IsType<TextBlock>(canvas.Children[3]);
            Assert.Equal("DN 150 (50% v. Haupt-DN)", label.Text);
            Assert.Equal(128, Canvas.GetLeft(label), precision: 6);
            Assert.Equal(38, Canvas.GetTop(label), precision: 6);
            Assert.Equal(OverlayTags.Measure, label.Tag);
        });
    }

    [Fact]
    public void Render_returns_false_without_enough_points_or_visible_radius()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();

            Assert.False(CodingLateralCircleOverlayRenderer.Render(
                canvas,
                new OverlayGeometry
                {
                    ToolType = OverlayToolType.LateralCircle,
                    Points = [new NormalizedPoint(0.5, 0.5)]
                },
                isPreview: false,
                effect: null,
                tag: OverlayTags.Manual,
                labelTag: OverlayTags.Manual,
                toPixel: ToPixel));

            Assert.False(CodingLateralCircleOverlayRenderer.Render(
                canvas,
                new OverlayGeometry
                {
                    ToolType = OverlayToolType.LateralCircle,
                    Points =
                    [
                        new NormalizedPoint(0.5, 0.5),
                        new NormalizedPoint(0.505, 0.5)
                    ]
                },
                isPreview: false,
                effect: null,
                tag: OverlayTags.Manual,
                labelTag: OverlayTags.Manual,
                toPixel: ToPixel));

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
