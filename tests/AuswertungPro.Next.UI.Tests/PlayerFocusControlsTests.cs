using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerFocusControlsTests
{
    [Fact]
    public void FocusElement_returns_wpf_focus_result()
    {
        RunOnStaThread(() =>
        {
            var button = new Button { Focusable = false };

            var result = PlayerFocusControls.FocusElement(button);

            Assert.False(result);
        });
    }

    [Fact]
    public void FocusElement_throws_for_null_element()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerFocusControls.FocusElement(null!));
    }

    [Fact]
    public void FocusWindowKeyboard_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerFocusControls.FocusWindowKeyboard(null!));
    }

    [Fact]
    public void ActivateWindow_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerFocusControls.ActivateWindow(null!));
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
