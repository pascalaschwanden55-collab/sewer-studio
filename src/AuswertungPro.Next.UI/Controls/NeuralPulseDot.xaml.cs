using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Kleiner KI-Puls: ein ruhender Punkt, um den ein Ring nach aussen verklingt, solange gearbeitet wird.
///
/// Bewusst zurueckhaltend: Der Kern steht still, nur der Ring bewegt sich, und zwischen zwei Ringen
/// liegt eine Pause — ein Herzschlag, kein Blinker. Ohne <see cref="IsActive"/> bleibt der Punkt grau
/// und vollstaendig ruhig.
///
/// Der Ring laeuft nur, wenn das Element sichtbar ist und der Nutzer Dauer-Animationen nicht
/// abgeschaltet hat (<see cref="MotionSettings"/>). Sichtbarkeitswechsel und Entladen stoppen ihn.
/// </summary>
public partial class NeuralPulseDot : UserControl
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(NeuralPulseDot),
            new PropertyMetadata(false, static (d, _) => ((NeuralPulseDot)d).UpdateState()));

    /// <summary>Farbe von Kern und Ring im aktiven Zustand. Standard: Akzentfarbe des Themes.</summary>
    public static readonly DependencyProperty DotBrushProperty =
        DependencyProperty.Register(nameof(DotBrush), typeof(Brush), typeof(NeuralPulseDot),
            new PropertyMetadata(null, static (d, _) => ((NeuralPulseDot)d).UpdateState()));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public Brush? DotBrush
    {
        get => (Brush?)GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    private Storyboard? _pulse;

    public NeuralPulseDot()
    {
        InitializeComponent();

        Loaded += (_, _) => UpdateState();
        Unloaded += (_, _) => StopPulse();
        IsVisibleChanged += (_, _) => UpdateState();
        UpdateState();
    }

    private void UpdateState()
    {
        var active = IsActive;

        // SetResourceReference statt fester Brush-Zuweisung: so folgen die Farben einem
        // Theme-Wechsel zur Laufzeit — das imperative Gegenstueck zu DynamicResource.
        if (active && DotBrush is not null)
        {
            Core.Fill = DotBrush;
            PulseRing.Stroke = DotBrush;
        }
        else if (active)
        {
            Core.SetResourceReference(Shape.FillProperty, "AccentBrush");
            PulseRing.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
        }
        else
        {
            Core.SetResourceReference(Shape.FillProperty, "MutedBrush");
        }

        if (active && IsVisible && !MotionSettings.ReduceMotion)
            StartPulse();
        else
            StopPulse();
    }

    private void StartPulse()
    {
        if (_pulse is not null)
            return;

        // 1.6 s Ring, danach 0.8 s Ruhe: die Pause macht aus dem Blinken einen Herzschlag.
        var grow = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        grow.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        grow.KeyFrames.Add(new EasingDoubleKeyFrame(1.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.6)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        grow.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.4))));

        var fade = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.6)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.4))));

        var scaleX = grow;
        var scaleY = grow.Clone();

        Storyboard.SetTarget(scaleX, PulseRing);
        Storyboard.SetTargetProperty(scaleX,
            new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        Storyboard.SetTarget(scaleY, PulseRing);
        Storyboard.SetTargetProperty(scaleY,
            new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        Storyboard.SetTarget(fade, PulseRing);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));

        _pulse = new Storyboard();
        _pulse.Children.Add(scaleX);
        _pulse.Children.Add(scaleY);
        _pulse.Children.Add(fade);
        _pulse.Begin();
    }

    private void StopPulse()
    {
        if (_pulse is null)
            return;

        _pulse.Stop();
        _pulse = null;
        PulseRing.Opacity = 0;
    }
}
