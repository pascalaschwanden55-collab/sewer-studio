using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Controls;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ButtonFxTests
{
    [Fact]
    public void PlayPressFeedback_ohne_AdornerLayer_zeigt_sichtbaren_Glanz_am_Button()
    {
        RunOnSta(() =>
        {
            var button = new Button
            {
                Width = 140,
                Height = 40
            };

            ButtonFx.PlayPressFeedback(button, new Point(70, 20));

            var glow = Assert.IsType<DropShadowEffect>(button.Effect);
            Assert.Equal(0, glow.ShadowDepth);
            Assert.True(glow.BlurRadius >= 18);
            Assert.True(glow.Opacity >= 0.65);
        });
    }

    [Fact]
    public void PlayPressFeedback_mehrfach_stellt_vorhandenen_Effekt_wieder_her()
    {
        RunOnSta(() =>
        {
            var resting = new DropShadowEffect
            {
                BlurRadius = 8,
                Opacity = 0.25
            };
            var button = new Button { Effect = resting };

            ButtonFx.PlayPressFeedback(button, new Point(10, 10));
            ButtonFx.PlayPressFeedback(button, new Point(20, 10));
            PumpDispatcherFor(TimeSpan.FromMilliseconds(500));

            Assert.Same(resting, button.Effect);
        });
    }

    private static void PumpDispatcherFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action action)
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
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
