using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Animations-Timeline fuer <see cref="GridLength"/>, damit z. B. die
/// Sidebar-Spalte (ColumnDefinition.Width) sanft ein-/ausgeblendet werden kann.
/// Interpoliert nur den numerischen Wert; GridUnitType kommt aus <see cref="To"/>.
/// </summary>
public class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation));

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

    public static readonly DependencyProperty EasingFunctionProperty =
        DependencyProperty.Register(nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

    /// <summary>Startwert der Animation (optional; ohne Angabe wird vom aktuellen Wert animiert).</summary>
    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    /// <summary>Zielwert der Animation.</summary>
    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    /// <summary>Easing-Funktion fuer weichere Uebergaenge (optional).</summary>
    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        // Ohne explizites From vom aktuellen Wert aus animieren.
        var fromVal = ReadLocalValue(FromProperty) == DependencyProperty.UnsetValue
            ? (GridLength)defaultOriginValue
            : From;
        var toVal = To;

        if (animationClock.CurrentProgress == null)
            return fromVal;

        var progress = animationClock.CurrentProgress.Value;
        if (EasingFunction != null)
            progress = EasingFunction.Ease(progress);

        var value = fromVal.Value + (toVal.Value - fromVal.Value) * progress;
        return new GridLength(value, toVal.GridUnitType);
    }
}
