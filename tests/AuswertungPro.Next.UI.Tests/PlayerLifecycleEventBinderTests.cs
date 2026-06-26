using System.ComponentModel;
using System.Threading;
using System.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerLifecycleEventBinderTests
{
    [Fact]
    public void Bind_registers_loaded_handlers()
    {
        RunOnStaThread(() =>
        {
            var window = new Window();
            var calls = new List<string>();

            PlayerLifecycleEventBinder.Bind(
                window,
                (_, _) => calls.Add("ensure-visible"),
                (_, _) => calls.Add("deactivated"),
                (_, _) => calls.Add("activated"),
                (_, _) => calls.Add("closing"),
                (_, _) => calls.Add("loaded"),
                (_, _) => calls.Add("closed"));

            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Equal(["ensure-visible", "loaded"], calls);
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
