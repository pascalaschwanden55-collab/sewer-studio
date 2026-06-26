using System.Threading;
using System.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerApplicationControlsTests
{
    [Fact]
    public void CurrentMainWindow_returns_resolved_window()
    {
        RunOnStaThread(() =>
        {
            var window = new Window();

            var result = PlayerApplicationControls.CurrentMainWindow(() => window);

            Assert.Same(window, result);
        });
    }

    [Fact]
    public void CurrentMainWindow_returns_null_when_resolver_returns_null()
    {
        Assert.Null(PlayerApplicationControls.CurrentMainWindow(() => null));
    }

    [Fact]
    public void CurrentMainWindow_throws_for_null_resolver()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerApplicationControls.CurrentMainWindow(null!));
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
