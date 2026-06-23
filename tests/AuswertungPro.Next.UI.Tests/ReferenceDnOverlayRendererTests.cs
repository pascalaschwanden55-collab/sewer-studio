using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ReferenceDnOverlayRendererTests
{
    [Fact]
    public void Render_replaces_ref_dn_overlay_with_circle_and_label()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border { Tag = OverlayTags.RefDn });
            var unrelated = new Border { Tag = OverlayTags.Manual };
            canvas.Children.Add(unrelated);

            var rendered = ReferenceDnOverlayRenderer.Render(
                canvas,
                Calibration(),
                showReferenceDn: true,
                canvasWidth: 1000,
                canvasHeight: 500);

            Assert.True(rendered);
            Assert.Contains(unrelated, canvas.Children.Cast<UIElement>());
            var refDnElements = canvas.Children
                .OfType<FrameworkElement>()
                .Where(e => Equals(e.Tag, OverlayTags.RefDn))
                .ToList();
            Assert.Equal(2, refDnElements.Count);

            var circle = Assert.IsType<Ellipse>(refDnElements[0]);
            Assert.Equal(300, circle.Width, precision: 6);
            Assert.Equal(300, circle.Height, precision: 6);
            Assert.Equal(350, Canvas.GetLeft(circle), precision: 6);
            Assert.Equal(100, Canvas.GetTop(circle), precision: 6);
            Assert.Equal(1.5, circle.StrokeThickness);
            Assert.Equal(6, circle.StrokeDashArray[0]);
            Assert.Equal(3, circle.StrokeDashArray[1]);
            Assert.IsType<SolidColorBrush>(circle.Stroke);

            var label = Assert.IsType<TextBlock>(refDnElements[1]);
            Assert.Equal("Ref: DN 300", label.Text);
            Assert.Equal(654, Canvas.GetLeft(label), precision: 6);
            Assert.Equal(242, Canvas.GetTop(label), precision: 6);
            Assert.Equal(11, label.FontSize);
            Assert.IsType<SolidColorBrush>(label.Foreground);
        });
    }

    [Fact]
    public void Render_clears_ref_dn_overlay_when_hidden()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border { Tag = OverlayTags.RefDn });
            var unrelated = new Border { Tag = OverlayTags.Manual };
            canvas.Children.Add(unrelated);

            var rendered = ReferenceDnOverlayRenderer.Render(
                canvas,
                Calibration(),
                showReferenceDn: false,
                canvasWidth: 1000,
                canvasHeight: 500);

            Assert.False(rendered);
            Assert.Single(canvas.Children);
            Assert.Same(unrelated, canvas.Children[0]);
        });
    }

    private static PipeCalibration Calibration()
        => new()
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.60,
            PipeCenter = new NormalizedPoint(0.5, 0.5),
            Source = CalibrationSource.Manual
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
