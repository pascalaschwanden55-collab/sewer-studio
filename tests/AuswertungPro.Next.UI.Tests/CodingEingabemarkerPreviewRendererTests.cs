using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerPreviewRendererTests
{
    [Fact]
    public void Create_adds_zero_sized_lime_preview_rectangle_at_drag_start()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();

            var rect = CodingEingabemarkerPreviewRenderer.Create(canvas, new Point(20, 30));

            Assert.Same(rect, canvas.Children[0]);
            Assert.Equal(0, rect.Width);
            Assert.Equal(0, rect.Height);
            Assert.Equal(20, Canvas.GetLeft(rect));
            Assert.Equal(30, Canvas.GetTop(rect));
            Assert.Same(Brushes.Lime, rect.Stroke);
            Assert.Equal(2, rect.StrokeThickness);
            Assert.Equal(4, rect.StrokeDashArray[0]);
            Assert.Equal(2, rect.StrokeDashArray[1]);
            var fill = Assert.IsType<SolidColorBrush>(rect.Fill);
            Assert.Equal(Color.FromArgb(40, 0, 255, 0), fill.Color);
        });
    }

    [Fact]
    public void Update_applies_preview_rect_position_and_size()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var rect = CodingEingabemarkerPreviewRenderer.Create(canvas, new Point(20, 30));

            CodingEingabemarkerPreviewRenderer.Update(rect, new Rect(12, 14, 50, 60));

            Assert.Equal(12, Canvas.GetLeft(rect));
            Assert.Equal(14, Canvas.GetTop(rect));
            Assert.Equal(50, rect.Width);
            Assert.Equal(60, rect.Height);
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
