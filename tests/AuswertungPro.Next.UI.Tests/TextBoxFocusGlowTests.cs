using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft den Fokus-Schein am echten Theme: Ein Effekt in einem ControlTemplate kann eingefroren
/// werden, dann wuerde die Einblendung zur Laufzeit werfen — an jedem Eingabefeld der Anwendung.
/// Die Tests setzen den Fokus wirklich und lassen die Animation laufen.
/// </summary>
public sealed class TextBoxFocusGlowTests
{
    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Focused_field_glows_and_returns_to_rest_when_focus_leaves(string themeFile)
    {
        RunOnSta(() =>
        {
            var (field, other) = CreateFocusPair(themeFile);
            var glow = (DropShadowEffect)field.Template.FindName("FocusGlow", field);

            Assert.False(glow.IsFrozen);
            Assert.Equal(0d, glow.Opacity, 3);

            field.Focus();
            Settle();
            Assert.Equal(0.45, glow.Opacity, 3);

            other.Focus();
            Settle();
            Assert.Equal(0d, glow.Opacity, 3);
        });
    }

    [Fact]
    public void Each_field_glows_on_its_own()
    {
        RunOnSta(() =>
        {
            // Ein geteilter Effekt liesse beim Fokussieren alle Felder zugleich leuchten.
            var (field, other) = CreateFocusPair("ThemeLight.xaml");
            var fieldGlow = (DropShadowEffect)field.Template.FindName("FocusGlow", field);
            var otherGlow = (DropShadowEffect)other.Template.FindName("FocusGlow", other);

            Assert.NotSame(fieldGlow, otherGlow);

            field.Focus();
            Settle();

            Assert.Equal(0.45, fieldGlow.Opacity, 3);
            Assert.Equal(0d, otherGlow.Opacity, 3);
        });
    }

    /// <summary>Zwei Felder in einem gezeigten Fenster — sonst laesst sich Fokus nicht wegnehmen.</summary>
    private static (TextBox Field, TextBox Other) CreateFocusPair(string themeFile)
    {
        using var stream = File.OpenRead(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));
        var theme = (ResourceDictionary)XamlReader.Load(stream);

        var field = new TextBox { Width = 160 };
        var other = new TextBox { Width = 160 };
        var panel = new StackPanel();
        panel.Children.Add(field);
        panel.Children.Add(other);

        var window = new Window
        {
            Width = 240,
            Height = 160,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            Content = panel
        };
        window.Resources.MergedDictionaries.Add(theme);
        window.Show();
        Settle();

        return (field, other);
    }

    private static void Settle()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
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
