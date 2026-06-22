using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingToolBadgeRendererTests
{
    [Fact]
    public void Update_replaces_existing_tool_badge_and_keeps_other_elements()
    {
        Exception? threadError = null;
        int childCount = -1;
        string? badgeText = null;
        object? otherTag = null;
        double badgeLeft = double.NaN;
        double badgeTop = double.NaN;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                canvas.Children.Add(new Border { Tag = OverlayTags.ToolBadge });
                var other = new Border { Tag = OverlayTags.BendMarker };
                canvas.Children.Add(other);

                CodingToolBadgeRenderer.Update(canvas, "Flaeche");

                childCount = canvas.Children.Count;
                otherTag = canvas.Children[0] is Border kept ? kept.Tag : null;
                var badge = Assert.IsType<Border>(canvas.Children[1]);
                badgeText = Assert.IsType<TextBlock>(badge.Child).Text;
                badgeLeft = Canvas.GetLeft(badge);
                badgeTop = Canvas.GetTop(badge);
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
        Assert.Equal(2, childCount);
        Assert.Equal(OverlayTags.BendMarker, otherTag);
        Assert.Equal("Flaeche", badgeText);
        Assert.Equal(10, badgeLeft);
        Assert.Equal(10, badgeTop);
    }

    [Fact]
    public void Update_clears_tool_badge_when_text_is_missing()
    {
        Exception? threadError = null;
        int childCount = -1;
        object? remainingTag = null;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                canvas.Children.Add(new Border { Tag = OverlayTags.ToolBadge });
                canvas.Children.Add(new Border { Tag = OverlayTags.BendMarker });

                CodingToolBadgeRenderer.Update(canvas, null);

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
        Assert.Equal(OverlayTags.BendMarker, remainingTag);
    }
}
