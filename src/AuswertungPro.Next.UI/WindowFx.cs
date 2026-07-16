using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Laesst ein Fenster beim Oeffnen sanft auftreten statt hart aufzuspringen.
/// Verwendung: <c>ui:WindowFx.Entrance="True"</c> am Fenster (Muster wie <see cref="Fluent"/>).
///
/// Laeuft immer — auch bei reduzierter Bewegung: Es ist eine kurze, einmalige Rueckmeldung auf
/// eine Nutzeraktion, kein Dauer-Effekt.
///
/// Nicht fuer Video- und Player-Fenster gedacht (Renderlast) und nicht fuer den Startbildschirm,
/// der eine eigene Choreografie hat.
/// </summary>
public static class WindowFx
{
    public static readonly DependencyProperty EntranceProperty = DependencyProperty.RegisterAttached(
        "Entrance", typeof(bool), typeof(WindowFx), new PropertyMetadata(false, OnEntranceChanged));

    public static void SetEntrance(DependencyObject element, bool value) => element.SetValue(EntranceProperty, value);

    public static bool GetEntrance(DependencyObject element) => (bool)element.GetValue(EntranceProperty);

    private static void OnEntranceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window)
            return;

        window.Loaded -= OnWindowLoaded;

        if (e.NewValue is true)
            window.Loaded += OnWindowLoaded;
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window)
            return;

        window.Loaded -= OnWindowLoaded;
        Play(window);
    }

    private static void Play(Window window)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(AnimationTokens.Slow);

        // Der Inhalt waechst minimal auf: am Fenster selbst wuerde das den Rahmen mitzerren.
        if (window.Content is FrameworkElement content && CanTakeTransform(content))
        {
            var scale = new ScaleTransform(0.985, 0.985);
            content.RenderTransformOrigin = new Point(0.5, 0.5);
            content.RenderTransform = scale;

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, duration) { EasingFunction = ease });
        }

        window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
    }

    /// <summary>
    /// Fremde Transformationen bleiben unangetastet — dann blendet das Fenster nur ein.
    /// Identity ist der Standardwert und darf ersetzt werden.
    /// </summary>
    private static bool CanTakeTransform(FrameworkElement content)
        => content.RenderTransform is null || ReferenceEquals(content.RenderTransform, Transform.Identity);
}
