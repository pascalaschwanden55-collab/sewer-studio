using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayDotMarkerRendererTests
{
    [Fact]
    public void Add_places_centered_dot_marker_on_canvas()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var fill = Brushes.Orange;

            var dot = CodingOverlayDotMarkerRenderer.Add(
                canvas,
                new Point(30, 40),
                radius: 6,
                fill,
                tag: OverlayTags.Preview,
                effect: null);

            Assert.Same(dot, Assert.Single(canvas.Children));
            Assert.Equal(12, dot.Width);
            Assert.Equal(12, dot.Height);
            Assert.Same(fill, dot.Fill);
            Assert.Same(Brushes.White, dot.Stroke);
            Assert.Equal(1.5, dot.StrokeThickness);
            Assert.Equal(OverlayTags.Preview, dot.Tag);
            Assert.Equal(24, Canvas.GetLeft(dot));
            Assert.Equal(34, Canvas.GetTop(dot));
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
