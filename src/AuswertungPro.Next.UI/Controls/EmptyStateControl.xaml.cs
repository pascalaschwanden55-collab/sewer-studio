using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AuswertungPro.Next.UI.Controls;

public partial class EmptyStateControl : UserControl
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(EmptyStateControl), new PropertyMetadata("\uE946"));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyStateControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(EmptyStateControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(EmptyStateControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(EmptyStateControl), new PropertyMetadata(null));

    private Storyboard? _float;

    public EmptyStateControl()
    {
        InitializeComponent();

        Loaded += (_, _) => UpdateFloat();
        IsVisibleChanged += (_, _) => UpdateFloat();
        Unloaded += (_, _) => StopFloat();
    }

    /// <summary>
    /// Laesst den Icon-Kreis ruhig schweben — aber nur, wenn er sichtbar ist und der Nutzer
    /// Dauer-Animationen nicht abgeschaltet hat.
    /// </summary>
    private void UpdateFloat()
    {
        if (IsVisible && !MotionSettings.ReduceMotion)
            StartFloat();
        else
            StopFloat();
    }

    private void StartFloat()
    {
        if (_float is not null)
            return;

        // Vier Sekunden hin und zurueck: langsam genug, dass es beim Lesen nicht stoert.
        var drift = new DoubleAnimation(0, -3, new Duration(TimeSpan.FromSeconds(2)))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(drift, IconCircle);
        Storyboard.SetTargetProperty(
            drift,
            new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

        _float = new Storyboard();
        _float.Children.Add(drift);
        _float.Begin();
    }

    private void StopFloat()
    {
        if (_float is null)
            return;

        _float.Stop();
        _float = null;
        IconCircleShift.Y = 0;
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }
}
