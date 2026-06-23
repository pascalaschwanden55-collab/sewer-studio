using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLevelOverlayRendererTests
{
    [Fact]
    public void Render_adds_standard_level_line_segment_and_label()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.Level,
                Points =
                [
                    new NormalizedPoint(0.2, 0.6),
                    new NormalizedPoint(0.8, 0.6)
                ],
                FillPercent = 30,
                LevelSubMode = LevelMode.Obstacle
            };

            var rendered = CodingLevelOverlayRenderer.Render(
                canvas,
                overlay,
                isPreview: true,
                effect: null,
                tag: OverlayTags.Preview,
                toPixel: ToPixel,
                calibration: Calibration(),
                canvasWidth: 200,
                canvasHeight: 100);

            Assert.True(rendered);
            Assert.Equal(4, canvas.Children.Count);
            Assert.IsType<Line>(canvas.Children[0]);
            Assert.IsType<Ellipse>(canvas.Children[1]);

            var segment = Assert.IsType<Rectangle>(canvas.Children[2]);
            Assert.Equal(60, segment.Width, precision: 6);
            Assert.Equal(40, segment.Height, precision: 6);
            Assert.Equal(70, Canvas.GetLeft(segment), precision: 6);
            Assert.Equal(20, Canvas.GetTop(segment), precision: 6);
            Assert.Equal(OverlayTags.Preview, segment.Tag);

            var label = Assert.IsType<TextBlock>(canvas.Children[3]);
            Assert.Equal("30.0%", label.Text);
            Assert.Equal(OverlayTags.Measure, label.Tag);
        });
    }

    [Fact]
    public void Render_adds_intrusion_shape_dot_and_label()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.Level,
                Points =
                [
                    new NormalizedPoint(0.4, 0.6),
                    new NormalizedPoint(0.5, 0.3),
                    new NormalizedPoint(0.5, 0.5),
                    new NormalizedPoint(0.45, 0.4),
                    new NormalizedPoint(0.55, 0.4)
                ],
                FillPercent = 44.2
            };

            var rendered = CodingLevelOverlayRenderer.Render(
                canvas,
                overlay,
                isPreview: false,
                effect: null,
                tag: OverlayTags.Manual,
                toPixel: ToPixel,
                calibration: null,
                canvasWidth: 200,
                canvasHeight: 100);

            Assert.True(rendered);
            Assert.Equal(5, canvas.Children.Count);
            Assert.IsType<Ellipse>(canvas.Children[0]);
            Assert.IsType<Polygon>(canvas.Children[1]);
            Assert.IsType<Line>(canvas.Children[2]);
            Assert.IsType<Ellipse>(canvas.Children[3]);
            var label = Assert.IsType<TextBlock>(canvas.Children[4]);
            Assert.Equal("Einragung 44.2%", label.Text);
            Assert.Equal(OverlayTags.Measure, label.Tag);
        });
    }

    private static PipeCalibration Calibration()
        => new()
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5),
            Source = CalibrationSource.Manual
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
