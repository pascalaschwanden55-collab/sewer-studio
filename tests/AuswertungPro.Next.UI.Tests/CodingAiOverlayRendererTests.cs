using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiOverlayRendererTests
{
    [Fact]
    public void Render_clears_existing_ai_children_and_renders_supported_ai_events()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border { Tag = OverlayTags.Manual });
            canvas.Children.Add(new Border { Tag = OverlayTags.AiOverlay });
            var events = new[]
            {
                AiEvent(
                    OverlayToolType.Line,
                    CodingUserDecision.Accepted,
                    "BAA",
                    confidence: 0.91,
                    (0.1, 0.2),
                    (0.3, 0.4)),
                AiEvent(
                    OverlayToolType.Rectangle,
                    CodingUserDecision.Rejected,
                    "BCA",
                    confidence: 0.42,
                    (0.2, 0.2),
                    (0.6, 0.2),
                    (0.6, 0.5),
                    (0.2, 0.5)),
                new CodingEvent
                {
                    Overlay = Geometry(OverlayToolType.Point, (0.5, 0.5)),
                    AiContext = null
                }
            };

            var rendered = CodingAiOverlayRenderer.Render(
                canvas,
                events,
                canvasWidth: 200,
                canvasHeight: 100,
                pipeCenter: new NormalizedPoint(0.5, 0.5),
                toPixel: ToPixel);

            Assert.Equal(2, rendered);
            Assert.Equal(4, canvas.Children.Count);
            Assert.Equal(OverlayTags.Manual, ((FrameworkElement)canvas.Children[0]).Tag);

            var line = Assert.IsType<Line>(canvas.Children[1]);
            var lineStroke = Assert.IsType<SolidColorBrush>(line.Stroke);
            Assert.Equal(Color.FromRgb(0x22, 0xC5, 0x5E), lineStroke.Color);
            Assert.Equal(OverlayTags.AiOverlay, line.Tag);

            var rect = Assert.IsType<Rectangle>(canvas.Children[2]);
            var rectStroke = Assert.IsType<SolidColorBrush>(rect.Stroke);
            Assert.Equal(Color.FromRgb(0xEF, 0x44, 0x44), rectStroke.Color);
            Assert.Equal(OverlayTags.AiOverlay, rect.Tag);

            var label = Assert.IsType<Border>(canvas.Children[3]);
            var labelText = Assert.IsType<TextBlock>(label.Child);
            Assert.Equal("BCA [42.0%]", labelText.Text);
        });
    }

    [Fact]
    public void Render_clears_existing_ai_children_before_ignoring_empty_canvas_size()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border { Tag = OverlayTags.AiOverlay });
            canvas.Children.Add(new Border { Tag = OverlayTags.Manual });

            var rendered = CodingAiOverlayRenderer.Render(
                canvas,
                [AiEvent(OverlayToolType.Point, CodingUserDecision.Ignored, "BCA", confidence: 0.1, (0.5, 0.5))],
                canvasWidth: 0,
                canvasHeight: 100,
                pipeCenter: new NormalizedPoint(0.5, 0.5),
                toPixel: ToPixel);

            Assert.Equal(0, rendered);
            Assert.Single(canvas.Children);
            Assert.Equal(OverlayTags.Manual, ((FrameworkElement)canvas.Children[0]).Tag);
        });
    }

    private static CodingEvent AiEvent(
        OverlayToolType tool,
        CodingUserDecision decision,
        string code,
        double confidence,
        params (double X, double Y)[] points)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            Overlay = Geometry(tool, points),
            AiContext = new CodingEventAiContext
            {
                Decision = decision,
                Confidence = confidence
            }
        };

    private static OverlayGeometry Geometry(OverlayToolType tool, params (double X, double Y)[] points)
        => new()
        {
            ToolType = tool,
            Points = points.Select(point => new NormalizedPoint(point.X, point.Y)).ToList()
        };

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
