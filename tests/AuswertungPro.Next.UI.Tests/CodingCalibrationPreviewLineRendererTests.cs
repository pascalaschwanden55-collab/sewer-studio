using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCalibrationPreviewLineRendererTests
{
    [Fact]
    public void Render_adds_tagged_magenta_dashed_preview_line()
    {
        Exception? threadError = null;
        int childCount = -1;
        double x1 = double.NaN;
        double y1 = double.NaN;
        double x2 = double.NaN;
        double y2 = double.NaN;
        double strokeThickness = double.NaN;
        Color strokeColor = default;
        double dash0 = double.NaN;
        double dash1 = double.NaN;
        object? tag = null;
        bool returnedCanvasChild = false;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                var preview = new CodingCalibrationPreviewState(
                    new Point(10, 20),
                    new Point(30, 45),
                    PixelLength: 32,
                    HintText: "Referenzlinie");

                var line = CodingCalibrationPreviewLineRenderer.Render(canvas, preview);
                var stroke = Assert.IsType<SolidColorBrush>(line.Stroke);

                childCount = canvas.Children.Count;
                returnedCanvasChild = ReferenceEquals(line, canvas.Children[0]);
                x1 = line.X1;
                y1 = line.Y1;
                x2 = line.X2;
                y2 = line.Y2;
                strokeThickness = line.StrokeThickness;
                strokeColor = stroke.Color;
                dash0 = line.StrokeDashArray[0];
                dash1 = line.StrokeDashArray[1];
                tag = line.Tag;
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        Assert.Equal(1, childCount);
        Assert.True(returnedCanvasChild);
        Assert.Equal(10, x1);
        Assert.Equal(20, y1);
        Assert.Equal(30, x2);
        Assert.Equal(45, y2);
        Assert.Equal(2.5, strokeThickness);
        Assert.Equal(Colors.Magenta, strokeColor);
        Assert.Equal(6, dash0);
        Assert.Equal(3, dash1);
        Assert.Equal(OverlayTags.Preview, tag);
    }
}
