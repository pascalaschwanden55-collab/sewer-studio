using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerKeyboardEventBinderTests
{
    [Fact]
    public void Bind_registers_preview_key_down_handler()
    {
        RunOnStaThread(() =>
        {
            var target = new Grid();
            using var source = new HwndSource(new HwndSourceParameters("keyboard-binder-test"));
            var calls = 0;

            PlayerKeyboardEventBinder.Bind(target, (_, _) => calls++);
            target.RaiseEvent(new KeyEventArgs(
                Keyboard.PrimaryDevice,
                source,
                Environment.TickCount,
                Key.D)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });

            Assert.Equal(1, calls);
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
