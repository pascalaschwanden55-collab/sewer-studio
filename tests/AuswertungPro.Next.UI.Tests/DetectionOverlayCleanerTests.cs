using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DetectionOverlayCleanerTests
{
    [Fact]
    public void ClearAll_clears_canvas_hides_overlay_and_clears_findings_source()
    {
        Exception? threadError = null;
        int childCount = -1;
        Visibility overlayVisibility = Visibility.Visible;
        object? itemsSource = new object();

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                canvas.Children.Add(new Border());
                var overlay = new Grid { Visibility = Visibility.Visible };
                var findings = new ListBox { ItemsSource = new[] { "BAB" } };

                DetectionOverlayCleaner.ClearAll(canvas, overlay, findings);

                childCount = canvas.Children.Count;
                overlayVisibility = overlay.Visibility;
                itemsSource = findings.ItemsSource;
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
        Assert.Equal(0, childCount);
        Assert.Equal(Visibility.Collapsed, overlayVisibility);
        Assert.Null(itemsSource);
    }

    [Fact]
    public void ClearVisuals_clears_canvas_and_hides_overlay_without_clearing_findings_source()
    {
        Exception? threadError = null;
        int childCount = -1;
        Visibility overlayVisibility = Visibility.Visible;
        object? itemsSource = null;

        var thread = new Thread(() =>
        {
            try
            {
                var existingItems = new[] { "BAB" };
                var canvas = new Canvas();
                canvas.Children.Add(new Border());
                var overlay = new Grid { Visibility = Visibility.Visible };
                var findings = new ListBox { ItemsSource = existingItems };

                DetectionOverlayCleaner.ClearVisuals(canvas, overlay);

                childCount = canvas.Children.Count;
                overlayVisibility = overlay.Visibility;
                itemsSource = findings.ItemsSource;
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
        Assert.Equal(0, childCount);
        Assert.Equal(Visibility.Collapsed, overlayVisibility);
        Assert.NotNull(itemsSource);
    }
}
