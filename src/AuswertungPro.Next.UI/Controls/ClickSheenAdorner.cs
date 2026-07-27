using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Einmaliger Klick-Glanz: expandierender, verblassender Akzent-Kreis an der Klickposition.
/// Wird von <see cref="ButtonFx"/> pro Klick angelegt und meldet sich nach Ablauf (~420 ms)
/// selbst zur Entfernung aus dem AdornerLayer.
///
/// Laeuft immer — auch bei reduzierter Bewegung: kurze, einmalige Rueckmeldung auf eine
/// Nutzeraktion, kein Dauer-Effekt (Muster wie WindowFx, vgl. MotionSettings).
/// </summary>
public sealed class ClickSheenAdorner : Adorner
{
    private const double StartRadius = 4d;
    private const double EndRadius = 54d;
    private const double RingThickness = 2d;

    // Deutlich sichtbar, aber kurz: rund 63 % Alpha in der Mitte.
    private const byte CenterAlpha = 0xA0;
    private const byte RingAlpha = 0xE0;

    private readonly Point _center;
    private readonly Color _accent;
    private readonly Action<ClickSheenAdorner> _onCompleted;

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ClickSheenAdorner),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>0 = Punkt am Klickort, 1 = voll expandiert und verblasst. Treibt OnRender.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <param name="onCompleted">Wird nach dem letzten Frame gerufen — Aufrufer entfernt den Adorner.</param>
    public ClickSheenAdorner(UIElement adornedElement, Point center, Color accent, Action<ClickSheenAdorner> onCompleted)
        : base(adornedElement)
    {
        _center = center;
        _accent = accent;
        _onCompleted = onCompleted;
        IsHitTestVisible = false;
    }

    /// <summary>Startet den einmaligen Ablauf; danach meldet sich der Adorner zum Entfernen.</summary>
    public void Play()
    {
        var animation = new DoubleAnimation(0d, 1d, new Duration(TimeSpan.FromMilliseconds(420)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        animation.Completed += (_, _) => _onCompleted(this);
        BeginAnimation(ProgressProperty, animation);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var progress = Progress;
        if (progress >= 1d)
            return;

        // Radius 4 -> 54 px, Deckkraft faellt ueber die Alphas auf 0.
        var radius = StartRadius + (EndRadius - StartRadius) * progress;
        var fade = 1d - progress;

        var fill = new RadialGradientBrush(
            Color.FromArgb((byte)(CenterAlpha * fade), _accent.R, _accent.G, _accent.B),
            Color.FromArgb(0, _accent.R, _accent.G, _accent.B));
        dc.DrawEllipse(fill, null, _center, radius, radius);

        // Duenner Ring am Rand; klingt etwas schneller aus als die Fuellung.
        var ringAlpha = (byte)(RingAlpha * Math.Max(0d, 1d - progress * 1.6));
        if (ringAlpha > 0)
        {
            var ring = new Pen(new SolidColorBrush(Color.FromArgb(ringAlpha, _accent.R, _accent.G, _accent.B)), RingThickness);
            dc.DrawEllipse(null, ring, _center, radius, radius);
        }
    }
}
