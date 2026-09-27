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

    [Fact]
    public void Render_places_ai_rectangle_line_and_point_inside_pillarboxed_video()
    {
        // B01 (Audit 23.09.2026): Ein 4:3-Video in einer 1600 x 900 grossen Flaeche hat links
        // und rechts je 200 Punkte Rand. Die KI-Markierung muss auf derselben Bildstelle liegen
        // wie im Video, also im tatsaechlichen Videorechteck und nicht in der ganzen Flaeche.
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var videoRect = CodingOverlayViewportMapper.GetContentRect(1600, 900, 4.0 / 3.0);
            Assert.Equal(200, videoRect.X, precision: 6);
            Assert.Equal(1200, videoRect.Width, precision: 6);

            var rendered = CodingAiOverlayRenderer.Render(
                canvas,
                [
                    AiEvent(
                        OverlayToolType.Rectangle,
                        CodingUserDecision.Accepted,
                        "BAB",
                        confidence: 0.9,
                        (0.2, 0.1),
                        (0.4, 0.1),
                        (0.4, 0.3),
                        (0.2, 0.3)),
                    AiEvent(OverlayToolType.Line, CodingUserDecision.Accepted, "BAB", 0.9, (0.1, 0.2), (0.3, 0.4)),
                    AiEvent(OverlayToolType.Point, CodingUserDecision.Accepted, "BCA", 0.9, (0.5, 0.5))
                ],
                canvasWidth: 1600,
                canvasHeight: 900,
                pipeCenter: new NormalizedPoint(0.5, 0.5),
                toPixel: point => CodingOverlayViewportMapper.NormToPixel(point, videoRect));

            Assert.Equal(3, rendered);

            var rect = canvas.Children.OfType<Rectangle>().Single();
            Assert.Equal(440, Canvas.GetLeft(rect), precision: 6);
            Assert.Equal(240, rect.Width, precision: 6);
            Assert.Equal(90, Canvas.GetTop(rect), precision: 6);
            Assert.Equal(180, rect.Height, precision: 6);

            var line = canvas.Children.OfType<Line>().Single();
            Assert.Equal(320, line.X1, precision: 6);
            Assert.Equal(180, line.Y1, precision: 6);
            Assert.Equal(560, line.X2, precision: 6);
            Assert.Equal(360, line.Y2, precision: 6);

            var dot = canvas.Children.OfType<Ellipse>().Single();
            Assert.Equal(800 - 7, Canvas.GetLeft(dot), precision: 6);
            Assert.Equal(450 - 7, Canvas.GetTop(dot), precision: 6);

            // Die Beschriftung haengt an der Box, nicht am Rand der ganzen Flaeche.
            var label = canvas.Children.OfType<Border>().Single();
            Assert.Equal(440, Canvas.GetLeft(label), precision: 6);
        });
    }

    [Fact]
    public void Render_draws_box_without_label_instead_of_throwing_when_canvas_is_smaller_than_label()
    {
        // B02 (Audit 23.09.2026): Vor dem ersten Layout ist die Zeichenflaeche nur wenige
        // Punkte gross. Die Beschriftung passt dann nicht hinein; das darf nicht werfen.
        RunOnStaThread(() =>
        {
            foreach (var size in new[] { 1.0, 20.0 })
            {
                var canvas = new Canvas();

                var rendered = CodingAiOverlayRenderer.Render(
                    canvas,
                    [
                        AiEvent(
                            OverlayToolType.Rectangle,
                            CodingUserDecision.Accepted,
                            "BAB Riss mit langer Beschriftung",
                            confidence: 0.9,
                            (0.2, 0.2),
                            (0.6, 0.2),
                            (0.6, 0.6),
                            (0.2, 0.6))
                    ],
                    canvasWidth: size,
                    canvasHeight: size,
                    pipeCenter: new NormalizedPoint(0.5, 0.5),
                    toPixel: point => new Point(point.X * size, point.Y * size));

                Assert.Equal(1, rendered);
                Assert.Single(canvas.Children.OfType<Rectangle>());
                Assert.Empty(canvas.Children.OfType<Border>());
            }
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
