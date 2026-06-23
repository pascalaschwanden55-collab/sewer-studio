using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayGeometryRendererTests
{
    [Fact]
    public void Render_adds_manual_rectangle_and_measurement_label()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.Rectangle,
                Q1Mm = 12.3,
                Points =
                [
                    new NormalizedPoint(0.2, 0.2),
                    new NormalizedPoint(0.6, 0.2),
                    new NormalizedPoint(0.6, 0.5),
                    new NormalizedPoint(0.2, 0.5)
                ]
            };

            var rendered = CodingOverlayGeometryRenderer.Render(
                canvas,
                overlay,
                isPreview: false,
                labelAnchor: new NormalizedPoint(0.4, 0.4),
                toPixel: ToPixel,
                calibration: null,
                canvasWidth: 200,
                canvasHeight: 100);

            Assert.True(rendered);
            Assert.Equal(2, canvas.Children.Count);

            var rect = Assert.IsType<Rectangle>(canvas.Children[0]);
            Assert.Equal(OverlayTags.Manual, rect.Tag);
            var stroke = Assert.IsType<SolidColorBrush>(rect.Stroke);
            Assert.Equal(Color.FromRgb(0x00, 0xE5, 0xFF), stroke.Color);

            var label = Assert.IsType<TextBlock>(canvas.Children[1]);
            Assert.Equal("Q1:12mm", label.Text);
            Assert.Equal(OverlayTags.Manual, label.Tag);
            Assert.Equal(92, Canvas.GetLeft(label));
            Assert.Equal(20, Canvas.GetTop(label));
        });
    }

    [Fact]
    public void Render_returns_false_without_valid_canvas_size()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();

            var rendered = CodingOverlayGeometryRenderer.Render(
                canvas,
                new OverlayGeometry
                {
                    ToolType = OverlayToolType.Line,
                    Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.4, 0.5)]
                },
                isPreview: true,
                labelAnchor: null,
                toPixel: ToPixel,
                calibration: null,
                canvasWidth: 0,
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
