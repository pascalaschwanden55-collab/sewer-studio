using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiRectangleOverlayRendererTests
{
    [Fact]
    public void Render_adds_ai_rectangle_and_label_with_mapped_bounds()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style();
            var overlay = Geometry(
                (0.8, 0.6),
                (0.8, 0.1),
                (0.2, 0.1),
                (0.2, 0.6));

            var rendered = CodingAiRectangleOverlayRenderer.Render(
                canvas,
                overlay,
                canvasWidth: 200,
                canvasHeight: 100,
                code: "BCA",
                confidence: 0.873,
                style);

            Assert.True(rendered);
            Assert.Equal(2, canvas.Children.Count);

            var rect = Assert.IsType<Rectangle>(canvas.Children[0]);
            Assert.Equal(40, Canvas.GetLeft(rect), precision: 6);
            Assert.Equal(10, Canvas.GetTop(rect), precision: 6);
            Assert.Equal(120, rect.Width, precision: 6);
            Assert.Equal(50, rect.Height, precision: 6);
            Assert.Same(style.Stroke, rect.Stroke);
            Assert.Equal(3, rect.StrokeThickness);
            Assert.Equal(6, rect.RadiusX);
            Assert.Equal(6, rect.RadiusY);
            Assert.Equal(style.Tag, rect.Tag);

            var fill = Assert.IsType<SolidColorBrush>(rect.Fill);
            Assert.Equal(Color.FromArgb(30, 245, 158, 11), fill.Color);

            var label = Assert.IsType<Border>(canvas.Children[1]);
            Assert.Equal(style.Tag, label.Tag);
            Assert.False(label.IsHitTestVisible);
            Assert.IsType<SolidColorBrush>(label.Background);
            var text = Assert.IsType<TextBlock>(label.Child);
            Assert.Equal("BCA [87.3%]", text.Text);
            Assert.Same(Brushes.White, text.Foreground);
            Assert.Equal(12, text.FontSize);
            Assert.Equal(FontWeights.Bold, text.FontWeight);
        });
    }

    [Fact]
    public void Render_returns_false_for_incomplete_or_non_rectangle_geometry()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var style = Style();

            Assert.False(CodingAiRectangleOverlayRenderer.Render(
                canvas,
                Geometry((0.1, 0.2), (0.3, 0.4)),
                200,
                100,
                "BCA",
                confidence: null,
                style));
            Assert.False(CodingAiRectangleOverlayRenderer.Render(
                canvas,
                Geometry(OverlayToolType.Line, (0.1, 0.2), (0.3, 0.4), (0.5, 0.6), (0.7, 0.8)),
                200,
                100,
                "BCA",
                confidence: null,
                style));
            Assert.Empty(canvas.Children);
        });
    }

    private static CodingAiRectangleOverlayRenderStyle Style()
        => new(
            Brushes.Orange,
            Color.FromRgb(0xF5, 0x9E, 0x0B),
            Effect: null,
            OverlayTags.AiOverlay);

    private static OverlayGeometry Geometry(params (double X, double Y)[] points)
        => Geometry(OverlayToolType.Rectangle, points);

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
