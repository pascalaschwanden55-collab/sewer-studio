using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Controls.Animations;

/// <summary>
/// Attached Behavior: Kinder eines Panels faden gestaffelt ein (Opacity + 12px Slide-up).
/// Deckel via StaggerDelayPolicy, deaktiviert sich bei VirtualizingPanel (dort wuerde
/// jedes Realisieren neu animieren). Fuer Code-Behind-Fluesse gibt es Play(panel),
/// z. B. beim Spaltenwechsel im VSA-Explorer.
/// Verwendung: anim:EntranceStagger.IsEnabled="True" auf StackPanel/Grid/WrapPanel.
/// </summary>
public static class EntranceStagger
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(EntranceStagger),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private const double SlideOffset = 12d;

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel || !(bool)e.NewValue)
            return;

        if (panel.IsLoaded)
            Play(panel);
        else
            panel.Loaded += (_, _) => Play(panel);
    }

    /// <summary>Staffelung manuell ausloesen (z. B. nach Neubefuellung einer Liste).</summary>
    public static void Play(Panel panel)
    {
        if (panel is VirtualizingPanel)
            return; // Virtualisierung: Realisieren wuerde staendig neu animieren.

        var index = 0;
        foreach (var child in panel.Children)
        {
            if (child is not FrameworkElement element || element.Visibility != Visibility.Visible)
                continue;

            AnimateChild(element, StaggerDelayPolicy.DelayFor(index));
            index++;
        }
    }

    private static void AnimateChild(FrameworkElement element, System.TimeSpan delay)
    {
        var translate = new TranslateTransform(0d, SlideOffset);
        element.RenderTransform = translate;
        element.Opacity = 0d;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0d, 1d, AnimationTokens.Normal)
        {
            BeginTime = delay,
            EasingFunction = ease
        };
        fade.Completed += (_, _) =>
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1d;
        };

        var slide = new DoubleAnimation(SlideOffset, 0d, AnimationTokens.Normal)
        {
            BeginTime = delay,
            EasingFunction = ease
        };
        slide.Completed += (_, _) =>
        {
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = 0d;
        };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}
