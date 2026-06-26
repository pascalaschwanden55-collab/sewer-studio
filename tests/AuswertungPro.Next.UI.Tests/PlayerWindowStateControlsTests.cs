using System.Threading;
using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowStateControlsTests
{
    [Fact]
    public void Track_delegates_to_track_window()
    {
        RunOnStaThread(() =>
        {
            var window = new Window();
            Window? trackedWindow = null;

            PlayerWindowStateControls.Track(window, tracked => trackedWindow = tracked);

            Assert.Same(window, trackedWindow);
        });
    }

    [Fact]
    public void Track_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerWindowStateControls.Track(null!, _ => { }));
    }

    [Fact]
    public void Track_throws_for_null_track_window()
    {
        RunOnStaThread(() =>
        {
            Assert.Throws<ArgumentNullException>(() => PlayerWindowStateControls.Track(new Window(), null!));
        });
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
