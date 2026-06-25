using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBendMarkerOverlayControllerTests
{
    [Fact]
    public void Show_renders_bend_marker_ring_and_label()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();

            CodingBendMarkerOverlayController.Show(
                canvas,
                vanishX: 0.5,
                vanishY: 0.5,
                contentRect: new Rect(10, 20, 200, 100));

            var ring = Assert.IsType<System.Windows.Shapes.Ellipse>(canvas.Children[0]);
            var label = Assert.IsType<TextBlock>(canvas.Children[1]);
            return (
                Count: canvas.Children.Count,
                RingTag: ring.Tag,
                LabelTag: label.Tag,
                Text: label.Text);
        });

        Assert.Equal(2, result.Count);
        Assert.Equal(OverlayTags.BendMarker, result.RingTag);
        Assert.Equal(OverlayTags.BendMarker, result.LabelTag);
        Assert.Equal("Bogen erkannt", result.Text);
    }

    [Fact]
    public void Clear_removes_only_bend_marker_elements()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border { Tag = OverlayTags.ToolBadge });

            CodingBendMarkerOverlayController.Show(
                canvas,
                vanishX: 0.5,
                vanishY: 0.5,
                contentRect: new Rect(0, 0, 100, 100));

            CodingBendMarkerOverlayController.Clear(canvas);

            return (canvas.Children.Count, ((Border)canvas.Children[0]).Tag);
        });

        Assert.Equal(1, result.Count);
        Assert.Equal(OverlayTags.ToolBadge, result.Tag);
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
