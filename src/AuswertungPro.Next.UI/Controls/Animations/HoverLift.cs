using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Controls.Animations;

/// <summary>
/// Attached Behavior: Karten/Kacheln heben sich beim Hover minimal an
/// (Scale 1.015 + 2px nach oben, 120 ms). Nur Transform-Animationen —
/// GPU-freundlich; Schatten werden bewusst NICHT animiert (CPU-Falle).
/// Verwendung: anim:HoverLift.IsEnabled="True" (nicht in virtualisierten Listen).
/// </summary>
public static class HoverLift
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(HoverLift),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private const double LiftScale = 1.015;
    private const double LiftOffsetY = -2d;

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        if ((bool)e.NewValue)
        {
            element.MouseEnter += OnMouseEnter;
            element.MouseLeave += OnMouseLeave;
        }
        else
        {
            element.MouseEnter -= OnMouseEnter;
            element.MouseLeave -= OnMouseLeave;
        }
    }

    private static void OnMouseEnter(object sender, RoutedEventArgs e)
        => Animate((FrameworkElement)sender, LiftScale, LiftOffsetY);

    private static void OnMouseLeave(object sender, RoutedEventArgs e)
        => Animate((FrameworkElement)sender, 1d, 0d);

    private static void Animate(FrameworkElement element, double scale, double offsetY)
    {
        var (scaleTransform, translateTransform) = EnsureTransforms(element);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(scale, AnimationTokens.Fast) { EasingFunction = ease });
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(scale, AnimationTokens.Fast) { EasingFunction = ease });
        translateTransform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(offsetY, AnimationTokens.Fast) { EasingFunction = ease });
    }

    private static (ScaleTransform Scale, TranslateTransform Translate) EnsureTransforms(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup existing
            && existing.Children.Count == 2
            && existing.Children[0] is ScaleTransform s
            && existing.Children[1] is TranslateTransform t)
            return (s, t);

        var scale = new ScaleTransform(1d, 1d);
        var translate = new TranslateTransform(0d, 0d);
        element.RenderTransform = new TransformGroup { Children = { scale, translate } };
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        return (scale, translate);
    }
}
