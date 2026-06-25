using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DetectionOverlayCleanupControllerTests
{
    [Fact]
    public void ClearAll_clears_visuals_and_findings_source()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border());
            var overlay = new Grid { Visibility = Visibility.Visible };
            var findings = new ListBox { ItemsSource = new[] { "BAB" } };

            DetectionOverlayCleanupController.ClearAll(canvas, overlay, findings);

            return (canvas.Children.Count, overlay.Visibility, findings.ItemsSource);
        });

        Assert.Equal(0, result.Count);
        Assert.Equal(Visibility.Collapsed, result.Visibility);
        Assert.Null(result.ItemsSource);
    }

    [Theory]
    [InlineData(true, Visibility.Collapsed)]
    [InlineData(false, Visibility.Visible)]
    public void ClearCanvas_hides_overlay_only_when_requested(bool hideOverlay, Visibility expectedVisibility)
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border());
            var overlay = new Grid { Visibility = Visibility.Visible };

            DetectionOverlayCleanupController.ClearCanvas(canvas, overlay, hideOverlay);

            return (canvas.Children.Count, overlay.Visibility);
        });

        Assert.Equal(0, result.Count);
        Assert.Equal(expectedVisibility, result.Visibility);
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
