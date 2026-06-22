using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayCanvasCleanerTests
{
    [Fact]
    public void ClearTransient_removes_transient_tags_and_keeps_stable_tags()
    {
        Exception? threadError = null;
        int childCount = -1;
        object? firstTag = null;
        object? secondTag = null;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                canvas.Children.Add(new Border { Tag = OverlayTags.ToolBadge });
                canvas.Children.Add(new Border { Tag = OverlayTags.Preview });
                canvas.Children.Add(new Border { Tag = OverlayTags.Measure });
                canvas.Children.Add(new Border { Tag = OverlayTags.Manual });
                canvas.Children.Add(new Border { Tag = OverlayTags.RefDn });
                canvas.Children.Add(new Border { Tag = OverlayTags.BendMarker });

                CodingOverlayCanvasCleaner.ClearTransient(canvas, clearManualOverlay: false);

                childCount = canvas.Children.Count;
                firstTag = canvas.Children[0] is Border first ? first.Tag : null;
                secondTag = canvas.Children[1] is Border second ? second.Tag : null;
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
        Assert.Equal(3, childCount);
        Assert.Equal(OverlayTags.Manual, firstTag);
        Assert.Equal(OverlayTags.RefDn, secondTag);
    }

    [Fact]
    public void ClearTransient_removes_manual_overlay_when_requested()
    {
        Exception? threadError = null;
        int childCount = -1;
        object? remainingTag = null;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                canvas.Children.Add(new Border { Tag = OverlayTags.Manual });
                canvas.Children.Add(new Border { Tag = OverlayTags.RefDn });

                CodingOverlayCanvasCleaner.ClearTransient(canvas, clearManualOverlay: true);

                childCount = canvas.Children.Count;
                remainingTag = canvas.Children[0] is Border kept ? kept.Tag : null;
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
        Assert.Equal(OverlayTags.RefDn, remainingTag);
    }
}
