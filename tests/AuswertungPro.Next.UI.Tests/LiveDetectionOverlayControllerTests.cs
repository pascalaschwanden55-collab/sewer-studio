using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionOverlayControllerTests
{
    [Fact]
    public void Render_clears_existing_children_and_renders_findings()
    {
        var result = RunOnSta(() =>
        {
            var clicked = false;
            var canvas = new Canvas
            {
                Width = 200,
                Height = 200
            };
            canvas.Arrange(new System.Windows.Rect(0, 0, 200, 200));
            canvas.Children.Add(new Border());

            LiveDetectionOverlayController.Render(
                canvas,
                [new LiveFrameFinding("Riss", 3, "3", 20)],
                timestampSec: 12.5,
                onFindingClicked: (_, _) => clicked = true);

            return (Count: canvas.Children.Count, Clicked: clicked);
        });

        Assert.True(result.Count > 1);
        Assert.False(result.Clicked);
    }

    [Fact]
    public void Render_clears_canvas_when_no_findings_exist()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas
            {
                Width = 200,
                Height = 200
            };
            canvas.Arrange(new System.Windows.Rect(0, 0, 200, 200));
            canvas.Children.Add(new Border());

            LiveDetectionOverlayController.Render(
                canvas,
                [],
                timestampSec: 12.5,
                onFindingClicked: (_, _) => { });

            return canvas.Children.Count;
        });

        Assert.Equal(0, result);
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
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
        return result!;
    }
}
