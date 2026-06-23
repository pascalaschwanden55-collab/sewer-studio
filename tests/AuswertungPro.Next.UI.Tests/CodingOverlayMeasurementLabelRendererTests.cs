using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayMeasurementLabelRendererTests
{
    [Fact]
    public void Add_places_measurement_label_with_overlay_style()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var anchor = new Point(40, 30);

            var label = CodingOverlayMeasurementLabelRenderer.Add(
                canvas,
                anchor,
                "12.3 mm",
                effect: null,
                tag: OverlayTags.Measure);

            Assert.Same(label, canvas.Children[0]);
            Assert.Equal("12.3 mm", label.Text);
            Assert.Equal(12, label.FontSize);
            Assert.Equal(FontWeights.SemiBold, label.FontWeight);
            Assert.Same(Brushes.White, label.Foreground);
            var background = Assert.IsType<SolidColorBrush>(label.Background);
            Assert.Equal(Color.FromArgb(200, 17, 19, 24), background.Color);
            Assert.Equal(new Thickness(5, 2, 5, 2), label.Padding);
            Assert.Equal(OverlayTags.Measure, label.Tag);
            Assert.Equal(52, Canvas.GetLeft(label));
            Assert.Equal(10, Canvas.GetTop(label));
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
