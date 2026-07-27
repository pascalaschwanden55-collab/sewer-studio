using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Klick-Glanz fuer alle Buttons: einmaliger Akzent-Kreis an der Klickposition (~420 ms,
/// <see cref="ClickSheenAdorner"/>). Einmalige Registrierung beim Programmstart aus
/// App.OnStartup via <see cref="RegisterGlobal"/> — bewusst NICHT als Style-Setter im
/// Theme: die Theme-Dictionaries werden in Tests roh per XamlReader gelesen, eigene Typen
/// darin wuerden die Aufloesung brechen; der Klassen-Handler gilt zudem fuer beide Themes.
///
/// Laeuft immer — auch bei reduzierter Bewegung: kurze, einmalige Rueckmeldung auf eine
/// Nutzeraktion, kein Dauer-Effekt (Muster wie WindowFx, vgl. MotionSettings).
/// </summary>
public static class ButtonFx
{
    // Gleiches Blau wie ColorAccent im Light-Theme, falls keine Resource greifbar ist.
    private static readonly Color FallbackAccent = Color.FromRgb(0x25, 0x63, 0xEB);
    private static readonly ConditionalWeakTable<Button, FallbackPulseState> FallbackPulseStates = new();
    private static bool _registered;

    /// <summary>Meldet den Klassen-Handler fuer alle Buttons an (idempotent).</summary>
    public static void RegisterGlobal()
    {
        if (_registered)
            return;
        _registered = true;
        EventManager.RegisterClassHandler(
            typeof(Button),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnButtonPress),
            handledEventsToo: true);
    }

    private static void OnButtonPress(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { IsEnabled: true } button)
            return;

        // Der gemeinsame Weg verwendet bei fehlender Adorner-Schicht den direkten Button-Glanz.
        PlayPressFeedback(button, e.GetPosition(button));
    }

    internal static void PlayPressFeedback(Button button, Point center)
    {
        var accent = button.TryFindResource("AccentBrush") is SolidColorBrush brush
            ? brush.Color
            : FallbackAccent;

        var layer = AdornerLayer.GetAdornerLayer(button);
        if (layer is not null)
        {
            var sheen = new ClickSheenAdorner(button, center, accent, self => layer.Remove(self));
            layer.Add(sheen);
            sheen.Play();
            return;
        }

        // Viele SewerStudio-Fenster besitzen keine Adorner-Schicht. Der direkte
        // Button-Glanz bleibt dort sichtbar und stellt einen vorhandenen Effekt wieder her.
        PlayFallbackPulse(button, accent);
    }

    private static void PlayFallbackPulse(Button button, Color accent)
    {
        var state = FallbackPulseStates.GetValue(button, static _ => new FallbackPulseState());
        state.Version++;
        state.RestoreTimer?.Stop();

        if (state.ActivePulse is not null)
        {
            state.ActivePulse.BeginAnimation(DropShadowEffect.OpacityProperty, null);
            if (ReferenceEquals(button.Effect, state.ActivePulse))
                button.SetCurrentValue(UIElement.EffectProperty, state.RestingEffect);
        }

        state.RestingEffect = button.Effect;
        var pulse = new DropShadowEffect
        {
            Color = accent,
            BlurRadius = 22,
            ShadowDepth = 0,
            Opacity = 0.82
        };
        state.ActivePulse = pulse;
        button.SetCurrentValue(UIElement.EffectProperty, pulse);

        var version = state.Version;
        var fade = new DoubleAnimation(
            fromValue: 0.82,
            toValue: 0,
            duration: new Duration(TimeSpan.FromMilliseconds(420)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        pulse.BeginAnimation(DropShadowEffect.OpacityProperty, fade);

        var restoreTimer = new DispatcherTimer(
            DispatcherPriority.Background,
            button.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(440)
        };
        state.RestoreTimer = restoreTimer;
        restoreTimer.Tick += (_, _) =>
        {
            restoreTimer.Stop();
            if (state.Version != version)
                return;

            state.RestoreTimer = null;
            if (!ReferenceEquals(button.Effect, pulse))
            {
                state.ActivePulse = null;
                state.RestingEffect = null;
                return;
            }

            pulse.BeginAnimation(DropShadowEffect.OpacityProperty, null);
            button.SetCurrentValue(UIElement.EffectProperty, state.RestingEffect);
            state.ActivePulse = null;
            state.RestingEffect = null;
        };
        restoreTimer.Start();
    }

    private sealed class FallbackPulseState
    {
        public int Version { get; set; }
        public Effect? RestingEffect { get; set; }
        public DropShadowEffect? ActivePulse { get; set; }
        public DispatcherTimer? RestoreTimer { get; set; }
    }
}
