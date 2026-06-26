using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSurfaceEventBinderTests
{
    [Fact]
    public void Bind_registers_detection_mouse_handler()
    {
        RunOnStaThread(() =>
        {
            var damageMarkerSurface = new Canvas();
            var heatmapSurface = new Canvas();
            var detectionSurface = new Canvas();
            var videoSurface = new Border();
            var window = new Window();
            using var source = new HwndSource(new HwndSourceParameters("surface-binder-test"));
            var calls = 0;

            PlayerSurfaceEventBinder.Bind(
                damageMarkerSurface,
                heatmapSurface,
                detectionSurface,
                videoSurface,
                window,
                (_, _) => { },
                (_, _) => { },
                (_, _) => calls++,
                (_, _) => { },
                (_, _) => { },
                (_, _) => { });

            detectionSurface.RaiseEvent(new MouseButtonEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = source.RootVisual
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
