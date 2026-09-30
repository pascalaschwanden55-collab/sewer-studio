using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Wartbarkeitsaudit 30.09.2026, Q3: Die gemeinsamen Stile (PageTitle, PrimaryButton, Caption, ...)
/// stehen einmal in Controls.xaml und werden beim Designwechsel NICHT mehr mit dem Theme
/// ausgetauscht. Was je Theme verschieden ist, muss deshalb per DynamicResource gelesen werden.
/// Dieser Test belegt das im echten WPF-Kindprozess mit den ECHTEN Anwendungsressourcen: Elemente in
/// einem echten Fenster, Theme hell -> dunkel -> hell wechseln, die Farbe muss jedes Mal mitwechseln
/// (eine Aenderung der App-Ressourcen erreicht lose Elemente nicht).
/// </summary>
[Collection("IsolatedWpf")]
public sealed class ThemeStileLiveWechselIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(ThemeStileLiveWechselIsolatedSmokeTests).FullName + "." + nameof(Kindprozess_Stile_folgen_dem_Designwechsel);

    [Fact]
    public async Task Gemeinsame_Stile_folgen_dem_Designwechsel_im_eigenen_Wpf_Prozess()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_Stile_folgen_dem_Designwechsel()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            ThemeManager.ApplyTheme(app.Resources, ThemeManager.Light);

            var titel = new TextBlock { Text = "Titel", Style = (Style)app.Resources["PageTitle"] };
            var beschriftung = new TextBlock { Text = "Klein", Style = (Style)app.Resources["Caption"] };
            var text = new TextBlock { Text = "Fliesstext", Style = (Style)app.Resources["Body"] };
            var primaer = new Button { Content = "Ok", Style = (Style)app.Resources["PrimaryButton"] };
            var sekundaer = new Button { Content = "Abbrechen", Style = (Style)app.Resources["SecondaryButton"] };
            var kompakt = new Button { Content = "Kompakt", Style = (Style)app.Resources["CompactButton"] };
            var gitter = new DataGrid { AutoGenerateColumns = false, Height = 80 };
            gitter.Columns.Add(new DataGridTextColumn { Header = "Spalte" });

            var ablage = new StackPanel();
            foreach (var element in new UIElement[] { titel, beschriftung, text, primaer, sekundaer, kompakt, gitter })
                ablage.Children.Add(element);

            // Alles in EINEM echten Fenster (siehe FuellknopfFarbenIsolatedSmokeTests).
            var fenster = new Window
            {
                Content = ablage, Width = 500, Height = 700, Left = -20000, Top = -20000,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            };
            WindowFx.SetEntrance(fenster, false);
            fenster.Show();
            fenster.UpdateLayout();

            Color Farbe(string key) => Assert.IsType<SolidColorBrush>(app.Resources[key]).Color;
            Color Unterstreichung()
            {
                var dekoration = Assert.Single(titel.TextDecorations!);
                var pinsel = Assert.IsType<LinearGradientBrush>(Assert.IsType<Pen>(dekoration.Pen).Brush);
                return pinsel.GradientStops[0].Color;
            }
            Color Vordergrund(Control c) => Assert.IsType<SolidColorBrush>(c.Foreground).Color;
            Color Text(TextBlock t) => Assert.IsType<SolidColorBrush>(t.Foreground).Color;

            void Pruefe(string design, Color erwarteteUnterstreichung)
            {
                fenster.UpdateLayout();
                Assert.Equal(erwarteteUnterstreichung, Unterstreichung());
                Assert.Equal(Farbe("TextBrush"), Text(titel));
                Assert.Equal(Farbe("TextSecondaryBrush"), Text(beschriftung));
                Assert.Equal(Farbe("TextBrush"), Text(text));
                Assert.Equal(Farbe("AccentBrush"), Assert.IsType<SolidColorBrush>(primaer.Background).Color);
                Assert.Equal(Farbe("OnAccentBrush"), Vordergrund(primaer));
                // Sekundaer/Kompakt erben von der impliziten Button-Vorlage (Theme.xaml): Die Vorlage
                // muss im neuen Theme gerendert werden, ohne Ausnahme.
                Assert.True(sekundaer.IsLoaded && kompakt.IsLoaded, design);
                Assert.Equal(28d, kompakt.MinHeight);
            }

            // Hell: kraeftiges Blau, dunkel: helleres Blau (siehe Theme.xaml/ThemeLight.xaml).
            var hell = Color.FromRgb(0x25, 0x63, 0xEB);
            var dunkel = Color.FromRgb(0x53, 0x9B, 0xF5);
            Pruefe("hell", hell);

            ThemeManager.ApplyTheme(app.Resources, ThemeManager.Dark);
            Pruefe("dunkel", dunkel);

            ThemeManager.ApplyTheme(app.Resources, ThemeManager.Light);
            Pruefe("hell (zurueck)", hell);

            // Die Kopfgriffe (KopfGriffOhneLinie/-Trennlinie) liegen jetzt in Controls.xaml: Die
            // Kopfvorlage in den Themes muss sie beim Instanziieren finden.
            gitter.UpdateLayout();
            var kopf = FindVisual<DataGridColumnHeader>(gitter);
            Assert.NotNull(kopf);
            kopf!.ApplyTemplate();
            var griff = kopf.Template.FindName("PART_RightHeaderGripper", kopf) as Thumb;
            Assert.NotNull(griff);
            Assert.Same(app.Resources["KopfGriffTrennlinie"], griff!.Style);
            Assert.Same(app.Resources["KopfGriffOhneLinie"], ((Thumb)kopf.Template.FindName("PART_LeftHeaderGripper", kopf)).Style);

            fenster.Close();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static T? FindVisual<T>(DependencyObject wurzel) where T : DependencyObject
    {
        var anzahl = VisualTreeHelper.GetChildrenCount(wurzel);
        for (var i = 0; i < anzahl; i++)
        {
            var kind = VisualTreeHelper.GetChild(wurzel, i);
            if (kind is T treffer)
                return treffer;
            var tiefer = FindVisual<T>(kind);
            if (tiefer is not null)
                return tiefer;
        }
        return null;
    }
}
