using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaOverlayRendererTests
{
    [Fact]
    public void AddPipeReference_draws_dashed_circle_from_normalized_center_and_radius()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var stroke = Brushes.Orange;

            var pipe = CodingSchemaOverlayRenderer.AddPipeReference(
                canvas,
                new NormalizedPoint(0.4, 0.6),
                radiusNorm: 0.2,
                canvasWidth: 200,
                canvasHeight: 100,
                stroke,
                effect: null,
                tag: OverlayTags.Preview);

            Assert.Same(pipe, Assert.Single(canvas.Children));
            Assert.Equal(40, pipe.Width);
            Assert.Equal(40, pipe.Height);
            Assert.Equal(60, Canvas.GetLeft(pipe));
            Assert.Equal(40, Canvas.GetTop(pipe));
            Assert.Same(stroke, pipe.Stroke);
            Assert.Equal(1.6, pipe.StrokeThickness);
            Assert.Equal(5, pipe.StrokeDashArray[0]);
            Assert.Equal(3, pipe.StrokeDashArray[1]);
            Assert.Equal(OverlayTags.Preview, pipe.Tag);
        });
    }

    [Fact]
    public void AddLabel_places_schema_label_next_to_anchor()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var foreground = Brushes.Lime;

            var label = CodingSchemaOverlayRenderer.AddLabel(
                canvas,
                new Point(50, 70),
                "42.0%",
                foreground,
                effect: null,
                tag: OverlayTags.Measure);

            Assert.Same(label, Assert.Single(canvas.Children));
            Assert.Equal("42.0%", label.Text);
            Assert.Equal(13, label.FontSize);
            Assert.Equal(FontWeights.SemiBold, label.FontWeight);
            Assert.Same(foreground, label.Foreground);
            Assert.Equal(OverlayTags.Measure, label.Tag);
            Assert.Equal(62, Canvas.GetLeft(label));
            Assert.Equal(50, Canvas.GetTop(label));
            var background = Assert.IsType<SolidColorBrush>(label.Background);
            Assert.Equal(Color.FromArgb(205, 17, 19, 24), background.Color);
        });
    }

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
