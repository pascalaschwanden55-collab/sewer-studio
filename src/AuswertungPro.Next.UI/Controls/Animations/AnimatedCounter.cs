using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Controls.Animations;

/// <summary>
/// TextBlock, der Zahlenwerte hochzaehlt statt sie hart zu setzen (Premium-KPI-Gefuehl).
/// Bindung auf <see cref="Value"/>; Anzeige ueber <see cref="StringFormat"/>
/// ("N0", "0.0" oder Composite wie "{0:N0} m"). Animiert nur nach dem Laden —
/// vorher wird der Text direkt gesetzt (kein Flackern in Dialogen).
/// </summary>
public sealed class AnimatedCounter : TextBlock
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(AnimatedCounter),
            new PropertyMetadata(0d, OnValueChanged));

    public static readonly DependencyProperty StringFormatProperty =
        DependencyProperty.Register(
            nameof(StringFormat), typeof(string), typeof(AnimatedCounter),
            new PropertyMetadata(null, (d, _) => ((AnimatedCounter)d).RenderValue()));

    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.Register(
            nameof(AnimationDuration), typeof(TimeSpan), typeof(AnimatedCounter),
            new PropertyMetadata(AnimationTokens.Slow));

    // Interner Laufwert der Animation; jede Aenderung schreibt den formatierten Text.
    private static readonly DependencyProperty AnimatedValueProperty =
        DependencyProperty.Register(
            "AnimatedValue", typeof(double), typeof(AnimatedCounter),
            new PropertyMetadata(0d, (d, _) => ((AnimatedCounter)d).RenderValue()));

    public AnimatedCounter()
    {
        // Premium-Ersteindruck: beim ersten Anzeigen von 0 auf den Zielwert zaehlen.
        Loaded += (_, _) => AnimateTo(Value, fromZero: true);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? StringFormat
    {
        get => (string?)GetValue(StringFormatProperty);
        set => SetValue(StringFormatProperty, value);
    }

    public TimeSpan AnimationDuration
    {
        get => (TimeSpan)GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var counter = (AnimatedCounter)d;
        if (!counter.IsLoaded)
        {
            // Vor dem Laden direkt setzen — animiert wird erst sichtbar.
            counter.BeginAnimation(AnimatedValueProperty, null);
            counter.SetValue(AnimatedValueProperty, (double)e.NewValue);
            return;
        }

        counter.AnimateTo((double)e.NewValue, fromZero: false);
    }

    private void AnimateTo(double target, bool fromZero)
    {
        var from = fromZero ? 0d : (double)GetValue(AnimatedValueProperty);
        if (Math.Abs(from - target) < double.Epsilon)
        {
            RenderValue();
            return;
        }

        var animation = new DoubleAnimation(from, target, AnimationDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        animation.Completed += (_, _) =>
        {
            // Clock freigeben und Endwert festschreiben (keine dauerhaft aktive Animation).
            BeginAnimation(AnimatedValueProperty, null);
            SetValue(AnimatedValueProperty, target);
        };
        BeginAnimation(AnimatedValueProperty, animation);
    }

    private void RenderValue()
        => Text = CounterTextFormatter.Format(
            (double)GetValue(AnimatedValueProperty), StringFormat, CultureInfo.CurrentCulture);
}
