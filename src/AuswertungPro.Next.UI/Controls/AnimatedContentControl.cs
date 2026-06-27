using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// ContentControl mit deutlich sichtbarem, aber ruhigem Seitenwechsel:
/// Fade + Slide-up + leichtes Zoom (Designstudie „Sichtbar-Pro").
/// </summary>
public class AnimatedContentControl : ContentControl
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(300);
    private readonly ScaleTransform _scale = new(0.98, 0.98);
    private readonly TranslateTransform _translate = new();

    public AnimatedContentControl()
    {
        var group = new TransformGroup();
        group.Children.Add(_scale);
        group.Children.Add(_translate);
        RenderTransform = group;
        RenderTransformOrigin = new Point(0.5, 0.5);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (newContent is null)
            return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Duration) { EasingFunction = ease });
        _translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(28, 0, Duration) { EasingFunction = ease });
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.98, 1, Duration) { EasingFunction = ease });
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.98, 1, Duration) { EasingFunction = ease });
    }
}
