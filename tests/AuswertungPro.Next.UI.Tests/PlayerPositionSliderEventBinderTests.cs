using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPositionSliderEventBinderTests
{
    [Fact]
    public void Bind_registers_slider_drag_handlers()
    {
        RunOnStaThread(() =>
        {
            var slider = new Slider();
            var started = 0;
            var completed = 0;

            PlayerPositionSliderEventBinder.Bind(
                slider,
                (_, _) => started++,
                (_, _) => completed++,
                (_, _) => { },
                (_, _) => { });

            slider.RaiseEvent(new DragStartedEventArgs(0, 0)
            {
                RoutedEvent = Thumb.DragStartedEvent
            });
            slider.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled: false)
            {
                RoutedEvent = Thumb.DragCompletedEvent
            });

            Assert.Equal(1, started);
            Assert.Equal(1, completed);
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
