using System.Threading;
using System.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerChromeControlsTests
{
    [Fact]
    public void EnableFocusable_sets_window_focusable()
    {
        RunOnStaThread(() =>
        {
            var window = new Window { Focusable = false };

            PlayerChromeControls.EnableFocusable(window);

            Assert.True(window.Focusable);
        });
    }

    [Fact]
    public void IsMinimized_returns_true_only_for_minimized_window()
    {
        RunOnStaThread(() =>
        {
            var window = new Window { WindowState = WindowState.Minimized };

            Assert.True(PlayerChromeControls.IsMinimized(window));

            window.WindowState = WindowState.Normal;

            Assert.False(PlayerChromeControls.IsMinimized(window));
            Assert.False(PlayerChromeControls.IsMinimized(null));
        });
    }

    [Fact]
    public void RestoreNormal_sets_normal_window_state()
    {
        RunOnStaThread(() =>
        {
            var window = new Window { WindowState = WindowState.Minimized };

            PlayerChromeControls.RestoreNormal(window);

            Assert.Equal(WindowState.Normal, window.WindowState);
        });
    }

    [Fact]
    public void EnableFocusable_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerChromeControls.EnableFocusable(null!));
    }

    [Fact]
    public void RestoreNormal_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerChromeControls.RestoreNormal(null!));
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
