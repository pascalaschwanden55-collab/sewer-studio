using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert den Hover-Lift dort, wo er wirklich brechen kann: Ein eingefrorener Schatten laesst
/// sich nicht animieren, und ein fremder Transform darf nicht ueberschrieben werden.
/// Die Tests loesen das Zeigen echt aus, statt nur Eigenschaften zu setzen.
/// </summary>
public sealed class HoverFxTests
{
    [Fact]
    public void Hovering_a_card_lifts_it_and_deepens_its_shadow()
    {
        RunOnSta(() =>
        {
            var card = HostCard(new Border());
            HoverFx.SetLift(card, true);

            RaiseMouseEnter(card);
            SettleAnimations();

            var transform = Assert.IsType<TranslateTransform>(card.RenderTransform);
            var shadow = Assert.IsType<DropShadowEffect>(card.Effect);

            Assert.False(shadow.IsFrozen);
            Assert.Equal(-2d, transform.Y, 3);
            Assert.Equal(0.22, shadow.Opacity, 3);
        });
    }

    [Fact]
    public void Leaving_a_card_returns_it_to_rest()
    {
        RunOnSta(() =>
        {
            var card = HostCard(new Border());
            HoverFx.SetLift(card, true);

            RaiseMouseEnter(card);
            SettleAnimations();
            RaiseMouseLeave(card);
            SettleAnimations();

            var transform = Assert.IsType<TranslateTransform>(card.RenderTransform);
            var shadow = Assert.IsType<DropShadowEffect>(card.Effect);

            Assert.Equal(0d, transform.Y, 3);
            Assert.Equal(0.10, shadow.Opacity, 3);
        });
    }

    [Fact]
    public void A_frozen_shadow_is_thawed_instead_of_throwing()
    {
        RunOnSta(() =>
        {
            // Genau der Fall aus dem Ressourcen-Woerterbuch: eingefroren und damit nicht animierbar.
            var frozen = new DropShadowEffect { BlurRadius = 9, Opacity = 0.5 };
            frozen.Freeze();

            var card = HostCard(new Border { Effect = frozen });
            HoverFx.SetLift(card, true);

            RaiseMouseEnter(card);
            SettleAnimations();

            var shadow = Assert.IsType<DropShadowEffect>(card.Effect);
            Assert.False(shadow.IsFrozen);
            // Die Gestaltung des Bestands bleibt erhalten, nur die Deckkraft bewegt sich.
            Assert.Equal(9, shadow.BlurRadius, 3);
            Assert.Equal(0.22, shadow.Opacity, 3);
        });
    }

    [Fact]
    public void A_foreign_transform_is_left_alone()
    {
        RunOnSta(() =>
        {
            var foreign = new RotateTransform(45);
            var card = HostCard(new Border { RenderTransform = foreign });
            HoverFx.SetLift(card, true);

            RaiseMouseEnter(card);
            SettleAnimations();

            // Lieber nur der Schatten als ein zerstoertes Layout.
            Assert.Same(foreign, card.RenderTransform);
            Assert.Equal(45d, foreign.Angle, 3);
            Assert.IsType<DropShadowEffect>(card.Effect);
        });
    }

    [Fact]
    public void Switching_lift_off_stops_reacting_to_hover()
    {
        RunOnSta(() =>
        {
            var card = HostCard(new Border());
            HoverFx.SetLift(card, true);
            HoverFx.SetLift(card, false);

            RaiseMouseEnter(card);
            SettleAnimations();

            Assert.Null(card.Effect);
        });
    }

    /// <summary>
    /// Haengt die Karte in ein gezeigtes Fenster. Ohne gerenderten Baum tickt die Animationsuhr
    /// nicht, und die Werte blieben auf ihrem Startwert stehen — der Test wuerde nichts pruefen.
    /// </summary>
    private static Border HostCard(Border card)
    {
        var window = new Window
        {
            Width = 200,
            Height = 120,
            // Ausserhalb des Bildschirms, damit der Testlauf nicht flackert.
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            Content = card
        };
        window.Show();
        return card;
    }

    /// <summary>Laesst den Dispatcher laufen, bis die kurzen Hover-Animationen durch sind.</summary>
    private static void SettleAnimations()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void RaiseMouseEnter(UIElement element)
        => element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });

    private static void RaiseMouseLeave(UIElement element)
        => element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
