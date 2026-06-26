using System.Threading;
using System.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerBoundsControlsTests
{
    [Fact]
    public void ApplyBounds_sets_window_bounds()
    {
        RunOnStaThread(() =>
        {
            var window = new Window
            {
                Left = 0,
                Top = 0,
                Width = 100,
                Height = 100
            };

            PlayerBoundsControls.ApplyBounds(window, new Rect(10, 20, 300, 200));

            Assert.Equal(10, window.Left);
            Assert.Equal(20, window.Top);
            Assert.Equal(300, window.Width);
            Assert.Equal(200, window.Height);
        });
    }

    [Fact]
    public void EnsureVisibleOnScreen_clamps_and_applies_work_area()
    {
        RunOnStaThread(() =>
        {
            var window = new Window
            {
                Left = -50,
                Top = -20,
                Width = 500,
                Height = 300
            };

            var bounds = PlayerBoundsControls.EnsureVisibleOnScreen(
                window,
                new Rect(10, 15, 1000, 800));

            Assert.Equal(new Rect(10, 15, 500, 300), bounds);
            Assert.Equal(10, window.Left);
            Assert.Equal(15, window.Top);
            Assert.Equal(500, window.Width);
            Assert.Equal(300, window.Height);
        });
    }

    [Fact]
    public void ApplyBounds_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerBoundsControls.ApplyBounds(null!, Rect.Empty));
    }

    [Fact]
    public void EnsureVisibleOnScreen_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerBoundsControls.EnsureVisibleOnScreen(null!, Rect.Empty));
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
