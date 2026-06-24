using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionPulseControlsTests
{
    [Fact]
    public void Start_sets_opacity_transform_and_repeating_animations()
    {
        RunOnStaThread(() =>
        {
            var pulseRing = new Ellipse { Opacity = 0 };

            LiveDetectionPulseControls.Start(pulseRing);

            Assert.Equal(1.0, pulseRing.Opacity);
            var scale = Assert.IsType<ScaleTransform>(pulseRing.RenderTransform);
            Assert.Equal(1, scale.ScaleX);
            Assert.Equal(1, scale.ScaleY);
            Assert.True(scale.HasAnimatedProperties);
            Assert.True(pulseRing.HasAnimatedProperties);
        });
    }

    [Fact]
    public void Stop_resets_opacity_and_existing_scale_transform()
    {
        RunOnStaThread(() =>
        {
            var scale = new ScaleTransform(1.4, 1.6);
            var pulseRing = new Ellipse
            {
                Opacity = 1,
                RenderTransform = scale
            };

            LiveDetectionPulseControls.Stop(pulseRing);

            Assert.Equal(0, pulseRing.Opacity);
            Assert.Same(scale, pulseRing.RenderTransform);
            Assert.Equal(1, scale.ScaleX);
            Assert.Equal(1, scale.ScaleY);
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
