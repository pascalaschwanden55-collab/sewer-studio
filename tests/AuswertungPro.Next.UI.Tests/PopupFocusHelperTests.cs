using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Fix-Runde 1 (Nova-Etappe 2b, Task 4): <see cref="PopupFocusHelper"/> setzt beim Oeffnen den
/// Fokus ins erste Feld und schliesst bei Escape wieder, mit Fokus zurueck an den Aufrufer.
/// Reale Fenster (off-screen), weil Fokus ohne echtes Fenster nicht zuverlaessig funktioniert.
/// </summary>
public sealed class PopupFocusHelperTests
{
    [Fact]
    public void FokussiereErstesFeld_setzt_den_Tastaturfokus()
    {
        RunOnSta(() =>
        {
            var (window, feld, anderesFeld) = ZweiFelderFenster();
            anderesFeld.Focus();
            Settle();
            Assert.True(anderesFeld.IsFocused);

            PopupFocusHelper.FokussiereErstesFeld(feld);
            Settle();

            Assert.True(feld.IsFocused);
            window.Close();
        });
    }

    [Fact]
    public void SchliesseBeiEscape_schliesst_das_Popup_und_gibt_den_Fokus_an_den_Menueknopf_zurueck()
    {
        RunOnSta(() =>
        {
            var (window, menuKnopf, popup, feldImPopup) = PopupSzenario();
            popup.IsOpen = true;
            Settle();
            feldImPopup.Focus();
            Settle();

            var behandelt = PopupFocusHelper.SchliesseBeiEscape(Key.Escape, popup, menuKnopf);

            Assert.True(behandelt);
            Assert.False(popup.IsOpen);
            Settle();
            Assert.True(menuKnopf.IsFocused);
            window.Close();
        });
    }

    [Fact]
    public void SchliesseBeiEscape_ignoriert_andere_Tasten_und_laesst_das_Popup_offen()
    {
        RunOnSta(() =>
        {
            var (window, menuKnopf, popup, _) = PopupSzenario();
            popup.IsOpen = true;
            Settle();

            var behandelt = PopupFocusHelper.SchliesseBeiEscape(Key.Enter, popup, menuKnopf);

            Assert.False(behandelt);
            Assert.True(popup.IsOpen);
            window.Close();
        });
    }

    private static (Window Window, TextBox Feld, TextBox AnderesFeld) ZweiFelderFenster()
    {
        var feld = new TextBox { Width = 120 };
        var anderesFeld = new TextBox { Width = 120 };
        var panel = new StackPanel();
        panel.Children.Add(feld);
        panel.Children.Add(anderesFeld);

        var window = new Window
        {
            Width = 200,
            Height = 160,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            Content = panel
        };
        window.Show();
        Settle();
        return (window, feld, anderesFeld);
    }

    private static (Window Window, Button MenuKnopf, Popup Popup, TextBox FeldImPopup) PopupSzenario()
    {
        var menuKnopf = new Button { Content = "Weitere Aktionen" };
        var feldImPopup = new TextBox { Width = 60 };
        var popup = new Popup { PlacementTarget = menuKnopf, Child = feldImPopup };
        var panel = new StackPanel();
        panel.Children.Add(menuKnopf);
        panel.Children.Add(popup);

        var window = new Window
        {
            Width = 200,
            Height = 160,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            Content = panel
        };
        window.Show();
        Settle();
        return (window, menuKnopf, popup, feldImPopup);
    }

    private static void Settle()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

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
