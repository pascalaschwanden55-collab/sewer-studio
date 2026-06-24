using System.Threading;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionToggleControlsTests
{
    [Fact]
    public void Uncheck_clears_checked_state()
    {
        RunOnStaThread(() =>
        {
            var toggle = new ToggleButton { IsChecked = true };

            LiveDetectionToggleControls.Uncheck(toggle);

            Assert.False(toggle.IsChecked);
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
