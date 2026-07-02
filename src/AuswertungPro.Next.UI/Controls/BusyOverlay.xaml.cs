using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Reines Bindungs-Control: zeigt einen halbtransparenten Lade-Layer mit Ring-Spinner,
/// Meldungstext und optionalem Abbrechen-Knopf. Steuerung ausschliesslich ueber die DPs
/// <see cref="IsActive"/>, <see cref="Message"/> und <see cref="CancelCommand"/> — kein Service,
/// keine Geschaeftslogik. Ueber ein <see cref="Services.BusyState"/> im ViewModel gebunden.
/// </summary>
public partial class BusyOverlay : UserControl
{
    private Storyboard? _spin;

    public BusyOverlay()
    {
        InitializeComponent();
    }

    // ── IsActive ──
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(BusyOverlay),
            new PropertyMetadata(false, OnIsActiveChanged));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    // ── Message ──
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(BusyOverlay),
            new PropertyMetadata(""));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    // ── CancelCommand (optional) ──
    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(BusyOverlay),
            new PropertyMetadata(null, OnCancelCommandChanged));

    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    private static void OnCancelCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BusyOverlay overlay)
            overlay.CancelButton.Visibility = e.NewValue is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BusyOverlay overlay)
            overlay.Apply((bool)e.NewValue);
    }

    private void Apply(bool active)
    {
        if (active)
        {
            ScrimRoot.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(1d, new Duration(AnimationTokens.Normal))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ScrimRoot.BeginAnimation(OpacityProperty, fadeIn);
            StartSpin();
        }
        else
        {
            StopSpin();
            var fadeOut = new DoubleAnimation(0d, new Duration(AnimationTokens.Fast))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (_, _) =>
            {
                if (!IsActive)
                    ScrimRoot.Visibility = Visibility.Collapsed;
            };
            ScrimRoot.BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    private void StartSpin()
    {
        if (_spin is null)
        {
            var rotate = new DoubleAnimation(0d, 360d, new Duration(TimeSpan.FromSeconds(0.9)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(rotate, SpinRotate);
            Storyboard.SetTargetProperty(rotate, new PropertyPath(RotateTransform.AngleProperty));
            _spin = new Storyboard();
            _spin.Children.Add(rotate);
        }

        _spin.Begin();
    }

    private void StopSpin() => _spin?.Stop();
}
