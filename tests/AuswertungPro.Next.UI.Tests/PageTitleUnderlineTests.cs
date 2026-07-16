using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft die Titel-Akzentlinie am echten Theme statt am Quelltext: Der Verlauf steckt in einem
/// Pen in einer TextDecoration — ein tief verschachtelter Freezable-Graph, bei dem eine falsche
/// Reihenfolge oder ein fehlender Schluessel erst zur Laufzeit auffiele.
/// </summary>
public sealed class PageTitleUnderlineTests
{
    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Page_title_gets_a_gradient_underline_with_room_to_breathe(string themeFile)
    {
        RunOnSta(() =>
        {
            var title = CreatePageTitle(themeFile);

            var decorations = title.TextDecorations;
            Assert.NotNull(decorations);
            var decoration = Assert.Single(decorations);

            Assert.Equal(TextDecorationLocation.Underline, decoration.Location);
            // Abstand zur Grundlinie: ohne ihn klebt die Linie am Text und wirkt wie ein Link.
            Assert.Equal(7d, decoration.PenOffset, 3);

            var pen = Assert.IsType<Pen>(decoration.Pen);
            Assert.Equal(2d, pen.Thickness, 3);

            // Der Verlauf laeuft nach rechts ins Transparente aus.
            var brush = Assert.IsType<LinearGradientBrush>(pen.Brush);
            Assert.Equal(3, brush.GradientStops.Count);
            Assert.Equal(0, brush.GradientStops[^1].Color.A);
        });
    }

    [Fact]
    public void The_underline_follows_the_theme_instead_of_staying_one_blue()
    {
        RunOnSta(() =>
        {
            var light = FirstStopColor(CreatePageTitle("ThemeLight.xaml"));
            var dark = FirstStopColor(CreatePageTitle("Theme.xaml"));

            // Das kraeftige Blau des Hellthemas verschwaende auf dunklem Grund.
            Assert.NotEqual(light, dark);
        });
    }

    private static Color FirstStopColor(TextBlock title)
    {
        var decoration = Assert.Single(title.TextDecorations!);
        var brush = Assert.IsType<LinearGradientBrush>(Assert.IsType<Pen>(decoration.Pen).Brush);
        return brush.GradientStops[0].Color;
    }

    /// <summary>Baut einen TextBlock mit dem echten PageTitle-Style aus dem angegebenen Theme.</summary>
    private static TextBlock CreatePageTitle(string themeFile)
    {
        using var stream = File.OpenRead(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));
        var theme = (ResourceDictionary)XamlReader.Load(stream);

        var title = new TextBlock { Text = "Projektuebersicht" };
        title.Resources.MergedDictionaries.Add(theme);
        title.Style = (Style)theme["PageTitle"];

        // Style-Setter greifen erst, wenn das Element seine Werte anwendet.
        title.Measure(new Size(500, 100));

        return title;
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
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
