using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft den Wartebalken am echten Template, nicht am Quelltext: Ein Verlaufspinsel in einem
/// ControlTemplate kann von WPF eingefroren werden — dann wirft die wandernde Animation zur
/// Laufzeit, und zwar an neun Stellen im Programm. Diese Tests loesen den Zustand wirklich aus.
/// </summary>
public sealed class ProgressBarIndeterminateTemplateTests
{
    [Fact]
    public void Indeterminate_sweep_runs_without_throwing_and_actually_moves()
    {
        RunOnSta(() =>
        {
            var bar = CreateThemedProgressBar(indeterminate: true);
            var sweepPeak = (GradientStop)bar.Template.FindName("SweepPeak", bar);

            // Eingefrorene Verlaufspinsel lassen sich nicht animieren — das ist die Falle.
            Assert.False(sweepPeak.IsFrozen);

            var start = sweepPeak.Offset;
            PumpFor(TimeSpan.FromMilliseconds(400));

            // Der Streif muss sich wirklich bewegt haben, nicht nur fehlerfrei dastehen.
            Assert.NotEqual(start, sweepPeak.Offset);
        });
    }

    [Fact]
    public void Indeterminate_shows_the_sweep_and_hides_the_determinate_track()
    {
        RunOnSta(() =>
        {
            var bar = CreateThemedProgressBar(indeterminate: true);

            var sweep = (Border)bar.Template.FindName("Sweep", bar);
            var track = (Border)bar.Template.FindName("PART_Track", bar);

            Assert.Equal(Visibility.Visible, sweep.Visibility);
            Assert.Equal(Visibility.Collapsed, track.Visibility);
        });
    }

    [Fact]
    public void Determinate_bar_keeps_the_track_and_leaves_the_sweep_hidden()
    {
        RunOnSta(() =>
        {
            var bar = CreateThemedProgressBar(indeterminate: false);
            bar.Value = 40;
            PumpFor(TimeSpan.FromMilliseconds(50));

            var sweep = (Border)bar.Template.FindName("Sweep", bar);
            var track = (Border)bar.Template.FindName("PART_Track", bar);

            Assert.Equal(Visibility.Collapsed, sweep.Visibility);
            Assert.Equal(Visibility.Visible, track.Visibility);
        });
    }

    /// <summary>Baut eine ProgressBar mit dem echten Controls.xaml-Style in einem gerenderten Fenster.</summary>
    private static ProgressBar CreateThemedProgressBar(bool indeterminate)
    {
        var controls = new ResourceDictionary();
        controls.Add(typeof(ProgressBar), LoadProgressBarStyleFromTheme());

        var bar = new ProgressBar
        {
            Width = 200,
            IsIndeterminate = indeterminate,
            Minimum = 0,
            Maximum = 100
        };

        // Ein echtes Fenster: Trigger und Storyboards greifen erst im gerenderten Baum.
        var window = new Window
        {
            Width = 300,
            Height = 100,
            // Ausserhalb des Bildschirms zeigen, damit der Testlauf nicht flackert.
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            Content = bar
        };
        window.Resources.MergedDictionaries.Add(controls);
        window.Show();
        PumpFor(TimeSpan.FromMilliseconds(50));

        return bar;
    }

    /// <summary>
    /// Schneidet den echten ProgressBar-Style aus Theme/Controls.xaml und parst nur ihn.
    ///
    /// Warum nicht die ganze Datei: Sie nutzt x:Shared, das nur in kompilierten Woerterbuechern
    /// erlaubt ist. Und nicht ueber pack: — dafuer braucht es eine laufende WPF-Anwendung.
    /// So wird die gepflegte Definition getestet und nicht eine Kopie im Test.
    /// </summary>
    private static Style LoadProgressBarStyleFromTheme()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Controls.xaml"));

        const string marker = "<Style TargetType=\"{x:Type ProgressBar}\">";
        var start = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "ProgressBar-Style wurde in Theme/Controls.xaml nicht gefunden.");

        const string closing = "</Style>";
        var end = xaml.IndexOf(closing, start, StringComparison.Ordinal);
        Assert.True(end > start, "ProgressBar-Style hat kein schliessendes Tag.");

        var styleXaml = xaml[start..(end + closing.Length)];
        var document =
            "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" "
            + "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
            + styleXaml
            + "</ResourceDictionary>";

        var dictionary = (ResourceDictionary)XamlReader.Parse(document);
        return (Style)dictionary[typeof(ProgressBar)];
    }

    /// <summary>Laesst den Dispatcher laufen, damit Animationen tatsaechlich Frames erzeugen.</summary>
    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration
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
