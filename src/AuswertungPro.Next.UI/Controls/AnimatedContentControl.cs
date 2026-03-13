using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// ContentControl mit sanftem Fade+Slide bei jedem Seitenwechsel.
/// </summary>
public class AnimatedContentControl : ContentControl
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(220);

    public AnimatedContentControl()
    {
        RenderTransform = new TranslateTransform();
        RenderTransformOrigin = new Point(0.5, 0.5);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (newContent is null)
            return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fadeIn = new DoubleAnimation(0, 1, Duration) { EasingFunction = ease };
        BeginAnimation(OpacityProperty, fadeIn);

        if (RenderTransform is TranslateTransform transform)
        {
            var slideUp = new DoubleAnimation(14, 0, Duration) { EasingFunction = ease };
            transform.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }
    }
}
