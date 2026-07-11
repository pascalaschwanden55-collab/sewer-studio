using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace AuswertungPro.Next.UI.Controls.Animations;

/// <summary>
/// Attached Behavior fuer KI-/Akzent-Elemente: dezenter Glow, der langsam pulsiert.
/// Der DropShadowEffect selbst bleibt STATISCH (Effect-Properties animieren = CPU-Falle);
/// animiert wird nur die Element-Opacity. Deshalb gehoert das Behavior auf ein
/// dediziertes Glow-Element (z. B. leerer Border hinter der Karte), nie auf Inhalte.
/// Verwendung: anim:GlowPulse.IsEnabled="True" (Farbe kommt aus GlowAccentColor).
/// </summary>
public static class GlowPulse
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(GlowPulse),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        if ((bool)e.NewValue)
        {
            if (element.IsLoaded)
                Start(element);
            else
                element.Loaded += (_, _) => Start(element);
        }
        else
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Effect = null;
            element.Opacity = 1d;
        }
    }

    private static void Start(FrameworkElement element)
    {
        var color = element.TryFindResource("GlowAccentColor") is Color themeColor
            ? themeColor
            : Color.FromRgb(0x25, 0x63, 0xEB);

        element.Effect = new DropShadowEffect
        {
            Color = color,
            ShadowDepth = 0d,
            BlurRadius = 16d,
            Opacity = 0.85d
        };

        var pulse = new DoubleAnimation(0.45d, 1d, System.TimeSpan.FromSeconds(1.6))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        element.BeginAnimation(UIElement.OpacityProperty, pulse);
    }
}
