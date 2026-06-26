using System.Threading;
using System.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerClipboardControlsTests
{
    [Fact]
    public void TryCopyWindowToClipboard_delegates_to_capture()
    {
        RunOnStaThread(() =>
        {
            var window = new Window();

            var result = PlayerClipboardControls.TryCopyWindowToClipboard(
                window,
                capturedWindow =>
                {
                    Assert.Same(window, capturedWindow);
                    return true;
                });

            Assert.True(result);
        });
    }

    [Fact]
    public void TryCopyWindowToClipboard_returns_delegate_result()
    {
        RunOnStaThread(() =>
        {
            var result = PlayerClipboardControls.TryCopyWindowToClipboard(
                new Window(),
                _ => false);

            Assert.False(result);
        });
    }

    [Fact]
    public void TryCopyWindowToClipboard_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerClipboardControls.TryCopyWindowToClipboard(
            null!,
            _ => true));
    }

    [Fact]
    public void TryCopyWindowToClipboard_throws_for_null_capture()
    {
        RunOnStaThread(() =>
        {
            Assert.Throws<ArgumentNullException>(() => PlayerClipboardControls.TryCopyWindowToClipboard(
                new Window(),
                null!));
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
