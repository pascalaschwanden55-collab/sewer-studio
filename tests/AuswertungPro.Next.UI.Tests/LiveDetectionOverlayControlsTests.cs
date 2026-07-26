using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionOverlayControlsTests
{
    [Fact]
    public void Show_makes_overlay_visible()
    {
        RunOnStaThread(() =>
        {
            var overlay = new Grid { Visibility = Visibility.Collapsed };

            LiveDetectionOverlayControls.Show(overlay);

            Assert.Equal(Visibility.Visible, overlay.Visibility);
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
