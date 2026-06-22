using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowBoundsPolicyTests
{
    [Fact]
    public void ClampToWorkArea_shrinks_window_that_is_larger_than_work_area()
    {
        var result = PlayerWindowBoundsPolicy.ClampToWorkArea(
            new Rect(0, 0, 1200, 900),
            new Rect(0, 0, 1000, 800));

        Assert.Equal(980, result.Width);
        Assert.Equal(780, result.Height);
    }

    [Fact]
    public void ClampToWorkArea_moves_window_inside_left_and_top_edges()
    {
        var result = PlayerWindowBoundsPolicy.ClampToWorkArea(
            new Rect(-50, -20, 500, 300),
            new Rect(10, 15, 1000, 800));

        Assert.Equal(10, result.Left);
        Assert.Equal(15, result.Top);
    }

    [Fact]
    public void ClampToWorkArea_moves_window_inside_right_and_bottom_edges()
    {
        var result = PlayerWindowBoundsPolicy.ClampToWorkArea(
            new Rect(900, 700, 200, 150),
            new Rect(0, 0, 1000, 800));

        Assert.Equal(800, result.Left);
        Assert.Equal(650, result.Top);
    }
}
