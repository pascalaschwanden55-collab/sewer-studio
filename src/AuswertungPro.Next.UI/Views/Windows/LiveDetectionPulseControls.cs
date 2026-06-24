using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class LiveDetectionPulseControls
{
    public static void Start(UIElement pulseRing)
    {
        ArgumentNullException.ThrowIfNull(pulseRing);

        pulseRing.Opacity = 1.0;
        if (pulseRing.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            pulseRing.RenderTransform = scale;
        }

        var scaleAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 2.2,
            Duration = TimeSpan.FromMilliseconds(900),
            RepeatBehavior = RepeatBehavior.Forever
        };
        var opacityAnim = new DoubleAnimation
        {
            From = 0.75,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(900),
            RepeatBehavior = RepeatBehavior.Forever
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        pulseRing.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }

    public static void Stop(UIElement pulseRing)
    {
        ArgumentNullException.ThrowIfNull(pulseRing);

        if (pulseRing.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        pulseRing.BeginAnimation(UIElement.OpacityProperty, null);
        pulseRing.Opacity = 0;
    }
}
