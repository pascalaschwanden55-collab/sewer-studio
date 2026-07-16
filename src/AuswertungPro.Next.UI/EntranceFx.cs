using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Laesst die Karten einer Seite nacheinander einschweben, statt alle zugleich aufblitzen zu lassen.
/// Verwendung: <c>ui:EntranceFx.Stagger="True"</c> auf einem Panel mit festen Karten.
///
/// Nur fuer ueberschaubare, feste Panels gedacht — nicht fuer datengebundene Listen: Bei
/// virtualisierten Elementen wuerde jedes Nachscrollen die Karten neu einschweben lassen.
/// Darum werden hoechstens <see cref="MaxStaggeredChildren"/> Kinder gestaffelt.
///
/// Laeuft einmalig auf ein Nutzer-Ereignis (Seitenwechsel) — kein Dauer-Effekt, darum auch bei
/// reduzierter Bewegung erlaubt.
/// </summary>
public static class EntranceFx
{
    /// <summary>Ab hier wird nicht weiter gestaffelt: Sonst warten die letzten Karten spuerbar.</summary>
    public const int MaxStaggeredChildren = 10;

    /// <summary>Versatz je Karte. Klein genug, dass die Seite als Ganzes ankommt, nicht als Reihe.</summary>
    public const double StaggerStepMs = 45;

    public static readonly DependencyProperty StaggerProperty = DependencyProperty.RegisterAttached(
        "Stagger", typeof(bool), typeof(EntranceFx), new PropertyMetadata(false, OnStaggerChanged));

    public static void SetStagger(DependencyObject element, bool value) => element.SetValue(StaggerProperty, value);

    public static bool GetStagger(DependencyObject element) => (bool)element.GetValue(StaggerProperty);

    /// <summary>
    /// Verzoegerung der Karte an Position <paramref name="index"/>.
    /// Oeffentlich, damit sie ohne Sonderzugriff pruefbar bleibt — Muster wie
    /// <see cref="Controls.AnimationTokens"/>.
    /// </summary>
    public static TimeSpan DelayFor(int index)
        => TimeSpan.FromMilliseconds(Math.Min(Math.Max(index, 0), MaxStaggeredChildren - 1) * StaggerStepMs);

    private static void OnStaggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel)
            return;

        panel.Loaded -= OnPanelLoaded;

        if (e.NewValue is true)
            panel.Loaded += OnPanelLoaded;
    }

    private static void OnPanelLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Panel panel)
            Play(panel);
    }

    private static void Play(Panel panel)
    {
        // Nur sichtbare Kinder zaehlen. Panels haben oft Karten mit Sichtbarkeits-Bindung, von
        // denen je Zustand nur wenige zu sehen sind — nach Rohposition gestaffelt bekaeme die
        // erste sichtbare Karte sonst die Verzoegerung der vierten und die Seite wirkte traege.
        var children = panel.Children
            .OfType<FrameworkElement>()
            .Where(child => child.Visibility == Visibility.Visible)
            .ToArray();

        for (var i = 0; i < children.Length; i++)
            PlayChild(children[i], DelayFor(i));
    }

    private static void PlayChild(FrameworkElement child, TimeSpan delay)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(AnimationTokens.Slow);

        child.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration) { BeginTime = delay, EasingFunction = ease });

        // Fremde Transformationen bleiben unangetastet — dann blendet die Karte nur ein.
        if (child.RenderTransform is not null && !ReferenceEquals(child.RenderTransform, Transform.Identity))
            return;

        var shift = new TranslateTransform();
        child.RenderTransform = shift;
        shift.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(12, 0, duration) { BeginTime = delay, EasingFunction = ease });
    }
}
