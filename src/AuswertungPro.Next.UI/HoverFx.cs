using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Hebt ein Element beim Zeigen leicht zum Betrachter und vertieft dabei seinen Schatten.
/// Verwendung: <c>ui:HoverFx.Lift="True"</c> auf einer klickbaren Karte.
///
/// Bewusst eine angehaengte Eigenschaft und kein Style: Die meisten Karten tragen bereits
/// <c>Style="{StaticResource Card}"</c>, und WPF laesst nur einen Style zu. So laesst sich der
/// Effekt zusaetzlich anschalten, ohne bestehende Styles umzubauen.
///
/// Nur fuer Karten, die zu einer Aktion fuehren — ein Lift ohne Klickziel verspricht etwas,
/// das nicht passiert.
/// </summary>
public static class HoverFx
{
    private const double RestY = 0d;
    private const double LiftY = -2d;
    private const double RestShadowOpacity = 0.10;
    private const double LiftShadowOpacity = 0.22;

    public static readonly DependencyProperty LiftProperty = DependencyProperty.RegisterAttached(
        "Lift", typeof(bool), typeof(HoverFx), new PropertyMetadata(false, OnLiftChanged));

    public static void SetLift(DependencyObject element, bool value) => element.SetValue(LiftProperty, value);

    public static bool GetLift(DependencyObject element) => (bool)element.GetValue(LiftProperty);

    private static void OnLiftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        element.MouseEnter -= OnMouseEnter;
        element.MouseLeave -= OnMouseLeave;

        if (e.NewValue is not true)
            return;

        element.MouseEnter += OnMouseEnter;
        element.MouseLeave += OnMouseLeave;
    }

    private static void OnMouseEnter(object sender, MouseEventArgs e)
        => Animate((FrameworkElement)sender, LiftY, LiftShadowOpacity, AnimationTokens.Fast);

    private static void OnMouseLeave(object sender, MouseEventArgs e)
        => Animate((FrameworkElement)sender, RestY, RestShadowOpacity, AnimationTokens.Normal);

    private static void Animate(FrameworkElement element, double y, double shadowOpacity, TimeSpan duration)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        if (EnsureLiftTransform(element) is { } transform)
        {
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(y, new Duration(duration)) { EasingFunction = ease });
        }

        EnsureAnimatableShadow(element).BeginAnimation(
            DropShadowEffect.OpacityProperty,
            new DoubleAnimation(shadowOpacity, new Duration(duration)) { EasingFunction = ease });
    }

    /// <summary>
    /// Liefert den eigenen Verschiebe-Transform des Elements; legt ihn beim ersten Mal an.
    /// Traegt das Element bereits einen fremden Transform, bleibt der unangetastet und es hebt
    /// sich nur der Schatten — lieber ein halber Effekt als ein zerstoertes Layout.
    /// </summary>
    private static TranslateTransform? EnsureLiftTransform(FrameworkElement element)
    {
        switch (element.RenderTransform)
        {
            case TranslateTransform existing when !existing.IsFrozen:
                return existing;
            case null:
            case TransformGroup { Children.Count: 0 }:
                break;
            default:
                // Identity ist der Standardwert und darf ersetzt werden; alles andere gehoert jemand anderem.
                if (!ReferenceEquals(element.RenderTransform, Transform.Identity))
                    return null;
                break;
        }

        var transform = new TranslateTransform();
        element.RenderTransform = transform;
        return transform;
    }

    /// <summary>
    /// Liefert einen animierbaren Schatten. Effekte aus einem Ressourcen-Woerterbuch koennen
    /// eingefroren sein — animieren wuerde dann zur Laufzeit werfen. Darum wird ein eingefrorener
    /// Schatten durch eine auftaubare Kopie ersetzt, und ein fehlender aus der zentralen
    /// ShadowS-Stufe geklont.
    /// </summary>
    private static DropShadowEffect EnsureAnimatableShadow(FrameworkElement element)
    {
        if (element.Effect is DropShadowEffect { IsFrozen: false } animatable)
            return animatable;

        if (element.Effect is DropShadowEffect frozen)
        {
            var thawed = frozen.Clone();
            element.Effect = thawed;
            return thawed;
        }

        var shadow = element.TryFindResource("ShadowS") is DropShadowEffect template
            ? template.Clone()
            : new DropShadowEffect
            {
                Color = Colors.Black,
                Opacity = RestShadowOpacity,
                BlurRadius = 8,
                ShadowDepth = 1,
                Direction = 270
            };

        element.Effect = shadow;
        return shadow;
    }
}
